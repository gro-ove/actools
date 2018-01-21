using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public enum FlatMirrorMode {
        TransparentMirror, BackgroundGround, SolidGround, ShadowOnlyGround, TextureMirror, TextureMirrorNoGround
    }

    // TODO: SORT OUT MODES!
    // I believe, at least one of them is not used
    public class FlatMirror : RenderableList {
        private abstract class FlatMirrorMaterialBase : IRenderableMaterial {
            protected EffectDarkMaterial Effect;

            public void EnsureInitialized(IDeviceContextHolder contextHolder) {
                if (Effect != null) return;
                Effect = contextHolder.GetEffect<EffectDarkMaterial>();
            }

            public void Refresh(IDeviceContextHolder contextHolder) {}

            public virtual bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.GBuffer) return false;
                contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPT;
                return true;
            }

            public void SetMatrices(Matrix objectTransform, ICamera camera) {
                Effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
                Effect.FxWorld.SetMatrix(objectTransform);
            }

            public abstract void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode);

            public virtual bool IsBlending => false;

            public void Dispose() { }
        }

        private class TransparentMirrorMaterial : FlatMirrorMaterialBase {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPT;
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.ReadOnlyDepthState;
                    return true;
                }

                if (mode != SpecialRenderMode.SimpleTransparent) return false;
                contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPT;
                contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.LessEqualReadOnlyDepthState;
                return true;
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror_SslrFix.DrawAllPasses(contextHolder.DeviceContext, indices);
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                    return;
                }

                Effect.TechFlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
            }

            public override bool IsBlending => true;
        }

        private class SemiTransparentGroundMaterial : FlatMirrorMaterialBase {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (!base.Prepare(contextHolder, mode)) return false;
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                return true;
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                    return;
                }

                Effect.TechFlatBackgroundGround.DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        private class SolidGroundMaterial : FlatMirrorMaterialBase {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (!base.Prepare(contextHolder, mode)) return false;
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                return true;
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                    return;
                }

                Effect.TechFlatAmbientGround.DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        private interface ITextureMirrorMaterial {
            void SetTextures(ShaderResourceView reflection, ShaderResourceView depth, ShaderResourceView normals);
        }

        private class ShadowOnlyGroundMaterial : FlatMirrorMaterialBase, ITextureMirrorMaterial {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    contextHolder.DeviceContext.OutputMerger.BlendState = null;
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                } else if (mode == SpecialRenderMode.Simple) {
                    contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.LessEqualReadOnlyDepthState;
                } else {
                    return false;
                }

                contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPT;
                return true;
            }

            public void SetTextures(ShaderResourceView reflection, ShaderResourceView depth, ShaderResourceView normals) {
                Effect.FxDiffuseMap.SetResource(reflection);
                //Effect.FxMapsMap.SetResource(depth);
                //Effect.FxNormalMap.SetResource(normals);
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                    return;
                }


                Effect.TechTransparentGround.DrawAllPasses(contextHolder.DeviceContext, indices);
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
            }
        }

        private class TextureMirrorMaterial : FlatMirrorMaterialBase, ITextureMirrorMaterial {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (!base.Prepare(contextHolder, mode)) return false;
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                return true;
            }

            public void SetTextures(ShaderResourceView reflection, ShaderResourceView depth, ShaderResourceView normals) {
                Effect.FxDiffuseMap.SetResource(reflection);
                //Effect.FxMapsMap.SetResource(depth);
                //Effect.FxNormalMap.SetResource(normals);
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                    return;
                }

                Effect.TechFlatTextureMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        private class TextureNoGroundMirrorMaterial : FlatMirrorMaterialBase, ITextureMirrorMaterial {
            public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                if (!base.Prepare(contextHolder, mode)) return false;
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                return true;
            }

            public void SetTextures(ShaderResourceView reflection, ShaderResourceView depth, ShaderResourceView normals) {
                Effect.FxDiffuseMap.SetResource(reflection);
                //Effect.FxMapsMap.SetResource(depth);
                //Effect.FxNormalMap.SetResource(normals);
            }

            public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                if (mode == SpecialRenderMode.GBuffer) {
                    Effect.TechGPass_FlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
                    return;
                }

                Effect.TechFlatTextureMirrorNoGround.DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        private class FlatMirrorObject : TrianglesRenderableObject<InputLayouts.VerticePT> {
            private static readonly InputLayouts.VerticePT[] BaseVertices;
            private static readonly ushort[] BaseIndices;

            static FlatMirrorObject() {
                BaseVertices = new InputLayouts.VerticePT[4];
                for (var i = 0; i < BaseVertices.Length; i++) {
                    BaseVertices[i] = new InputLayouts.VerticePT(
                            new Vector3(i < 2 ? 1 : -1, 0, i % 2 == 0 ? -1 : 1),
                            new Vector2(i < 2 ? 1 : 0, i % 2));
                }

                BaseIndices = new ushort[] { 0, 1, 2, 3, 2, 1 };
            }

            private FlatMirrorMaterialBase _material;

            public Matrix Transform;
            private FlatMirrorMode _mode;

            public FlatMirrorObject(Matrix transform, FlatMirrorMode mode) : base(null, BaseVertices, BaseIndices) {
                Transform = transform;
                _mode = mode;
            }

            protected override void Initialize(IDeviceContextHolder contextHolder) {
                base.Initialize(contextHolder);
                SetMode(contextHolder, _mode);
            }

            public void SetMode(IDeviceContextHolder contextHolder, FlatMirrorMode mode) {
                if (_material != null && _mode == mode) return;

                _mode = mode;
                DisposeHelper.Dispose(ref _material);

                switch (mode) {
                    case FlatMirrorMode.TransparentMirror:
                        _material = new TransparentMirrorMaterial();
                        break;
                    case FlatMirrorMode.SolidGround:
                        _material = new SolidGroundMaterial();
                        break;
                    case FlatMirrorMode.TextureMirror:
                        _material = new TextureMirrorMaterial();
                        break;
                    case FlatMirrorMode.TextureMirrorNoGround:
                        _material = new TextureNoGroundMirrorMaterial();
                        break;
                    case FlatMirrorMode.BackgroundGround:
                        _material = new SemiTransparentGroundMaterial();
                        break;
                    case FlatMirrorMode.ShadowOnlyGround:
                        _material = new ShadowOnlyGroundMaterial();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }

                _material.EnsureInitialized(contextHolder);
            }

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (!_material.Prepare(contextHolder, mode)) return;
                base.DrawOverride(contextHolder, camera, mode);

                _material.SetMatrices(Transform * ParentMatrix, camera);
                _material.Draw(contextHolder, Indices.Length, mode);
            }

            public override void Dispose() {
                base.Dispose();
                _material?.Dispose();
            }

            public void SetTexture(ShaderResourceView reflection, ShaderResourceView depth, ShaderResourceView normals) {
                var material = _material as ITextureMirrorMaterial;
                material?.SetTextures(reflection, depth, normals);
            }
        }

        private readonly FlatMirrorObject _object;

        private FlatMirror([CanBeNull] IRenderableObject mirroredObject, Plane plane, FlatMirrorMode mode) {
            LocalMatrix = Matrix.Reflection(plane);

            var point = plane.Normal * plane.D;
            var matrix = Matrix.Scaling(200f, 200f, 200f) * Matrix.Translation(point);

            if (mirroredObject != null) {
                MirroredObject = mirroredObject.Clone();
                Add(MirroredObject);
                _object = new FlatMirrorObject(matrix, mode) { ParentMatrix = Matrix };
            } else {
                _object = new FlatMirrorObject(matrix, mode) { ParentMatrix = Matrix };
            }
        }

        [CanBeNull]
        public IRenderableObject MirroredObject { get; }

        public FlatMirror([NotNull] IRenderableObject mirroredObject, Plane plane, bool noGround) : this(mirroredObject, plane,
                noGround ? FlatMirrorMode.ShadowOnlyGround : FlatMirrorMode.TransparentMirror) {}

        public FlatMirror(Plane plane, bool opaqueMode, bool noGround) : this(null, plane,
                noGround ? FlatMirrorMode.ShadowOnlyGround :
                        opaqueMode ? FlatMirrorMode.SolidGround : FlatMirrorMode.TransparentMirror) {}

        public override void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.GBuffer) return;
            _object.Draw(holder, camera, mode, filter);
        }

        public void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, ShaderResourceView reflected, ShaderResourceView depth, ShaderResourceView normals) {
            _object.SetTexture(reflected, depth, normals);
            _object.Draw(holder, camera, mode);
        }

        public void DrawReflection(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (camera != null && camera.Position.Y > 0) {
                try {
                    contextHolder.Get<DarkMaterialsParams>().IsMirrored = true;
                    base.Draw(contextHolder, camera, mode, filter);
                } finally {
                    contextHolder.Get<DarkMaterialsParams>().IsMirrored = false;
                }
            }
        }

        public void SetMode(IDeviceContextHolder contextHolder, FlatMirrorMode mode) {
            _object.SetMode(contextHolder, mode);
        }

        public override void Dispose() {
            base.Dispose();
            _object.Dispose();
        }
    }
}