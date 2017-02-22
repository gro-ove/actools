using System;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public sealed class Kn5SkinnedObject : TrianglesRenderableObject<InputLayouts.VerticePNTGW4B>, IKn5RenderableObject {
        public readonly bool IsCastingShadows;

        public Kn5Node OriginalNode { get; }

        private static InputLayouts.VerticePNTGW4B[] Convert(Kn5Node.Vertice[] vertices, Kn5Node.VerticeWeight[] weights) {
            var size = vertices.Length;
            var result = new InputLayouts.VerticePNTGW4B[size];
            
            for (var i = 0; i < size; i++) {
                var x = vertices[i];
                var w = weights[i];
                result[i] = new InputLayouts.VerticePNTGW4B(
                        x.Co.ToVector3(),
                        x.Normal.ToVector3(),
                        x.Uv.ToVector2(),
                        x.Tangent.ToVector3(),
                        w.Weights.ToVector3(),
                        w.Indices.Select(y => y < 0 ? 0 : y).ToArray().ToVector4());
            }

            return result;
        }

        private static ushort[] Convert(ushort[] indices) {
            return indices.ToIndicesFixX();
        }

        public Kn5SkinnedObject(Kn5Node node) : base(node.Name, Convert(node.Vertices, node.VerticeWeights), Convert(node.Indices)) {
            OriginalNode = node;
            IsCastingShadows = node.CastShadows;

            if (IsEnabled && (!OriginalNode.Active || !OriginalNode.IsVisible || !OriginalNode.IsRenderable)) {
                IsEnabled = false;
            }

            if (OriginalNode.IsTransparent) {
                IsReflectable = false;
            }
            
            _bonesTransform = node.Bones.Select(x => x.Transform.ToMatrix()).ToArray();
            _bones = _bonesTransform.ToArray();
        }

        private readonly Matrix[] _bones;
        private readonly Matrix[] _bonesTransform;
        private Kn5RenderableList[] _bonesNodes;

        private void UpdateNodes() {
            if (_bonesNodes == null) return;

            var bones = OriginalNode.Bones;
            for (var i = 0; i < bones.Length; i++) {
                var node = _bonesNodes[i];
                if (node != null) {
                    _bones[i] = _bonesTransform[i] * node.RelativeToModel;
                }
            }
        }

        private ISkinnedMaterial Material => _debugMaterial ?? _material;

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) {}
        
        [CanBeNull]
        private ISkinnedMaterial _debugMaterial;
        private bool _debugMaterialInitialized;

        public void SetDebugMode(IDeviceContextHolder holder, bool enabled) {
            if (enabled == (_debugMaterial != null)) return;

            if (enabled) {
                var material = holder.Get<SharedMaterials>().GetMaterial(new Tuple<object, uint>(BasicMaterials.DebugKey, OriginalNode.MaterialId));

                _debugMaterial = material as ISkinnedMaterial;
                if (_debugMaterial == null) {
                    AcToolsLogging.Write("Error: ISkinnedMaterial required for Kn5SkinnedObject!");
                    material.Dispose();
                    return;
                }

                if (IsInitialized) {
                    _debugMaterial.Initialize(holder);
                    _debugMaterialInitialized = true;
                }
            } else {
                _debugMaterialInitialized = false;
                DisposeHelper.Dispose(ref _debugMaterial);
            }
        }

        public Vector3? Emissive { get; set; }

        public void SetEmissive(Vector3? color) {
            Emissive = color;
        }

        private bool _isTransparent;
        private ISkinnedMaterial _material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            var material = contextHolder.Get<SharedMaterials>().GetMaterial(OriginalNode.MaterialId);
            _material = material as ISkinnedMaterial;
            if (_material == null) {
                AcToolsLogging.Write("Error: ISkinnedMaterial required for Kn5SkinnedObject!");
                material.Dispose();
                _material = new InvisibleMaterial();
            }

            _material.Initialize(contextHolder);
            _isTransparent = OriginalNode.IsTransparent && _material.IsBlending;

            var model = contextHolder.Get<IKn5Model>();
            _bonesNodes = OriginalNode.Bones.Select(x => model.GetDummyByName(x.Name)).ToArray();

            if (_debugMaterial != null && !_debugMaterialInitialized) {
                _debugMaterialInitialized = true;
                _debugMaterial.Initialize(contextHolder);
            }
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (_isTransparent &&
                    mode != SpecialRenderMode.Outline &&
                    mode != SpecialRenderMode.SimpleTransparent &&
                    mode != SpecialRenderMode.DeferredTransparentForw &&
                    mode != SpecialRenderMode.DeferredTransparentDef &&
                    mode != SpecialRenderMode.DeferredTransparentMask) return;

            if (mode == SpecialRenderMode.Shadow && !IsCastingShadows) return;

            var material = Material;
            if (!material.Prepare(contextHolder, mode)) return;

            base.DrawOverride(contextHolder, camera, mode);

            if (Emissive.HasValue) {
                (material as IEmissiveMaterial)?.SetEmissiveNext(Emissive.Value);
            }

            UpdateNodes();
            material.SetBones(_bones);
            material.SetMatrices(ParentMatrix, camera);
            material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            DisposeHelper.Dispose(ref _debugMaterial);
            base.Dispose();
        }

        /*
        public override BaseRenderableObject Clone() {
            return new ClonedKn5RenderableObject(this);
        }
        
        internal class ClonedKn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
            private readonly Kn5RenderableObject _original;

            internal ClonedKn5RenderableObject(Kn5RenderableObject original) : base(original.Name + "_copy", original.Vertices, original.Indices) {
                _original = original;
            }

            public override bool IsEnabled => _original.IsEnabled;

            public override bool IsReflectable => _original.IsReflectable;

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (_original._isTransparent &&
                        mode != SpecialRenderMode.Outline &&
                        mode != SpecialRenderMode.SimpleTransparent &&
                        mode != SpecialRenderMode.DeferredTransparentForw &&
                        mode != SpecialRenderMode.DeferredTransparentDef &&
                        mode != SpecialRenderMode.DeferredTransparentMask) return;

                if (mode == SpecialRenderMode.Shadow && !_original.IsCastingShadows || _original._material == null) return;

                var material = _original.Material;
                if (!material.Prepare(contextHolder, mode)) return;

                base.DrawOverride(contextHolder, camera, mode);

                if (_original.Emissive.HasValue) {
                    (material as IEmissiveMaterial)?.SetEmissiveNext(_original.Emissive.Value);
                }

                material.SetMatrices(ParentMatrix, camera);
                material.Draw(contextHolder, Indices.Length, mode);
            }

            public override BaseRenderableObject Clone() {
                return new ClonedKn5RenderableObject(_original);
            }
        }*/
    }
}