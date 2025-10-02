using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Render.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class DepthMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (BasicMaterials.DepthOnlyKey.Equals(key)) {
                /* Model is loaded directly without using Kn5RenderableFile as a wrapper, so all materials
                 * keys won’t be converted to Kn5MaterialDescription. We don’t need any information about
                 * materials anyway. */
                return new Kn5MaterialDepth();
            }

            return new InvisibleMaterial();
        }
    }

    public class Kn5MaterialDepth : IRenderableMaterial {
        private EffectSpecialShadow _effect;

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSpecialShadow>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPT;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechSimplest.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void PrepareAo(IDeviceContextHolder contextHolder, ShaderResourceView txNormal, float uvRepeat) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            _effect.FxNormalMap.SetResource(txNormal);
            _effect.FxNormalUvMult.Set(uvRepeat);
        }

        public void PrepareShadow(ShaderResourceView txNormal, float uvRepeat) {
            _effect.FxAlphaMap.SetResource(txNormal);
            _effect.FxAlphaRef.Set(uvRepeat);
        }

        public void SetMatricesAo(Matrix objectTransform) {
            _effect.FxWorld.SetMatrix(objectTransform);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Transpose(objectTransform).Invert_v2());
        }

        public void DrawAo(IDeviceContextHolder contextHolder, int indices) {
            _effect.TechAo.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public string Name => null;

        public void Dispose() { }
    }

    public interface IAlphaTexturesProvider : IDisposable {
        [CanBeNull]
        Tuple<IRenderableTexture, float> GetTexture(IDeviceContextHolder contextHolder, uint materialId);
    }

    public interface INormalsNormalTexturesProvider : IDisposable {
        [CanBeNull]
        Tuple<IRenderableTexture, float> GetTexture(IDeviceContextHolder contextHolder, uint materialId);
    }

    public sealed class Kn5RenderableDepthOnlyObject : TrianglesRenderableObject<InputLayouts.VerticePT>, IKn5RenderableObject {
        public Kn5Node OriginalNode { get; }
        public Matrix ModelMatrixInverted { get; set; }

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) { }
        public void SetDebugMode(IDeviceContextHolder holder, bool enabled) { }

        int IKn5RenderableObject.TrianglesCount => GetTrianglesCount();

        public void SetTransparent(bool? isTransparent) { }
        public void SetCastShadows(bool? castShadows) { }

        private TrianglesRenderableObject<InputLayouts.VerticePNTG> _pntgObject;

        private static ushort[] Convert(ushort[] indices) {
            return indices.ToIndicesFixX();
        }

        public Kn5RenderableDepthOnlyObject(Kn5Node node, bool forceVisible = false)
                : base(node.Name, InputLayouts.VerticePT.Convert(node.Vertices), Convert(node.Indices)) {
            OriginalNode = node;
            if (IsEnabled && (!node.Active || !forceVisible && (!node.IsVisible || !node.IsRenderable))) {
                IsEnabled = false;
            }
        }

        private IRenderableMaterial _material;

        [CanBeNull]
        private Kn5MaterialDepth _materialDepth;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = contextHolder.Get<SharedMaterials>().GetMaterial(BasicMaterials.DepthOnlyKey);
            _material.EnsureInitialized(contextHolder);
            _materialDepth = _material as Kn5MaterialDepth;
        }

        private Tuple<IRenderableTexture, float> _txNormal, _txAlpha;
        private ShaderResourceView _txNormalView, _txAlphaView;
        private bool _txAlphaSet;

        private static bool IsClockwise(Vector2 a, Vector2 b, Vector2 c){
            return (b.X - a.X) * (b.Y + a.Y) +
                    (c.X - b.X) * (c.Y + b.Y) +
                    (a.X - c.X) * (a.Y + c.Y) > 0f;
        }

        private static void FixClockwiseness(InputLayouts.VerticePNTG[] v, ref ushort a, ref ushort b, ref ushort c) {
            if (IsClockwise(v[a].Tex, v[b].Tex, v[c].Tex)) {
                var s = c;
                c = b;
                b = s;
            }
        }

        private static void FixClockwiseness(TrianglesRenderableObject<InputLayouts.VerticePNTG> obj) {
            for (var i = 2; i < obj.IndicesCount; i += 3) {
                FixClockwiseness(obj.Vertices, ref obj.Indices[i - 2], ref obj.Indices[i - 1], ref obj.Indices[i]);
            }
        }

        // TODO: Split to separate objects? Or not?
        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Shadow) {
                if (_pntgObject == null) {
                    _pntgObject = new TrianglesRenderableObject<InputLayouts.VerticePNTG>("",
                            InputLayouts.VerticePNTG.Convert(OriginalNode.Vertices), OriginalNode.Indices.Copy());
                    FixClockwiseness(_pntgObject);
                    _pntgObject.Draw(contextHolder, camera, SpecialRenderMode.InitializeOnly);

                    _txNormal = contextHolder.Get<INormalsNormalTexturesProvider>().GetTexture(contextHolder, OriginalNode.MaterialId);
                    _txNormalView = _txNormal?.Item1.Resource ?? contextHolder.GetFlatNmTexture();
                }

                if (_materialDepth == null) return;
                _materialDepth.PrepareAo(contextHolder, _txNormalView, _txNormal?.Item2 ?? 1f);
                _pntgObject.SetBuffers(contextHolder);
                _materialDepth.SetMatricesAo(ParentMatrix);
                _materialDepth.DrawAo(contextHolder, Indices.Length);
            } else {
                if (mode != SpecialRenderMode.Simple) return;
                if (!_material.Prepare(contextHolder, mode)) return;

                if (_materialDepth != null) {
                    if (!_txAlphaSet) {
                        _txAlphaSet = true;
                        _txAlpha = contextHolder.TryToGet<IAlphaTexturesProvider>()?.GetTexture(contextHolder, OriginalNode.MaterialId);
                        _txAlphaView = _txAlpha?.Item1.Resource;
                    }

                    if (_txAlpha != null) {
                        _materialDepth.PrepareShadow(_txAlphaView, _txAlphaView == null ? -1f : _txAlpha.Item2);
                    } else {
                        _materialDepth.PrepareShadow(null, -1f);
                    }
                }

                base.DrawOverride(contextHolder, camera, mode);

                _material.SetMatrices(ParentMatrix, camera);

                if (SeveralRasterizerStates != null) {
                    for (var i = 0; i < SeveralRasterizerStates.Length; i++) {
                        contextHolder.DeviceContext.Rasterizer.State = SeveralRasterizerStates[i];
                        _material.Draw(contextHolder, Indices.Length, mode);
                    }
                } else {
                    _material.Draw(contextHolder, Indices.Length, mode);
                }
            }
        }

        public RasterizerState[] SeveralRasterizerStates;

        public override BaseRenderableObject Clone() {
            throw new NotSupportedException();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _pntgObject);
            DisposeHelper.Dispose(ref _material);
            base.Dispose();
        }

        AcDynamicMaterialParams IKn5RenderableObject.DynamicMaterialParams { get; } = null;
    }

    public class Kn5DepthOnlyConverter : IKn5ToRenderableConverter {
        public static Kn5DepthOnlyConverter Instance { get; } = new Kn5DepthOnlyConverter();

        public IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, this);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableDepthOnlyObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public class Kn5DepthOnlyForceVisibleConverter : IKn5ToRenderableConverter {
        public static Kn5DepthOnlyForceVisibleConverter Instance { get; } = new Kn5DepthOnlyForceVisibleConverter();

        public IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, this);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableDepthOnlyObject(node, true);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}