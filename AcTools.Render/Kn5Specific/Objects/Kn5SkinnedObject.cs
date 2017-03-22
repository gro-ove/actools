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
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public sealed class Kn5SkinnedObject : TrianglesRenderableObject<InputLayouts.VerticePNTGW4B>, IKn5RenderableObject {
        public readonly bool IsCastingShadows;

        public Kn5Node OriginalNode { get; }

        public Matrix ModelMatrixInverted { get; set; }

        private bool _isTransparent;
        private readonly float _distanceFromSqr, _distanceToSqr;

        public Kn5SkinnedObject(Kn5Node node) : base(node.Name, InputLayouts.VerticePNTGW4B.Convert(node.Vertices, node.VerticeWeights),
                node.Indices.ToIndicesFixX()) {
            OriginalNode = node;
            IsCastingShadows = node.CastShadows;

            if (IsEnabled && (!OriginalNode.Active || !OriginalNode.IsVisible || !OriginalNode.IsRenderable)) {
                IsEnabled = false;
            }

            if (OriginalNode.IsTransparent || OriginalNode.Layer == 1 /* WHAT? WHAT DOES IT DO? BUT KUNOS PREVIEWS SHOWROOM WORKS THIS WAY, SO… */) {
                IsReflectable = false;
            }

            _bonesTransform = node.Bones.Select(x => x.Transform.ToMatrix()).ToArray();
            _bones = _bonesTransform.ToArray();

            _isTransparent = OriginalNode.IsTransparent;
            _distanceFromSqr = OriginalNode.LodIn.Pow(2f);
            _distanceToSqr = OriginalNode.LodOut.Pow(2f);
        }

        private readonly Matrix[] _bones;
        private readonly Matrix[] _bonesTransform;
        private Kn5RenderableList[] _bonesNodes;

        private void UpdateNodes() {
            if (_bonesNodes == null) return;

            var fix = Matrix.Invert(ParentMatrix * ModelMatrixInverted);
            var bones = OriginalNode.Bones;
            for (var i = 0; i < bones.Length; i++) {
                var node = _bonesNodes[i];
                if (node != null) {
                    _bones[i] = _bonesTransform[i] * node.RelativeToModel * fix;
                }
            }
        }

        private ISkinnedMaterial Material => _debugMaterial ?? _material;

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) { }

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

        int IKn5RenderableObject.TrianglesCount => GetTrianglesCount();

        public void SetTransparent(bool? isTransparent) {
            _isTransparent = isTransparent ?? OriginalNode.IsTransparent;
        }

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

            var model = contextHolder.Get<IKn5Model>();
            _bonesNodes = OriginalNode.Bones.Select(x => model.GetDummyByName(x.Name)).ToArray();

            if (_debugMaterial != null && !_debugMaterialInitialized) {
                _debugMaterialInitialized = true;
                _debugMaterial.Initialize(contextHolder);
            }
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!(_isTransparent ? Kn5RenderableObject.TransparentModes : Kn5RenderableObject.OpaqueModes).HasFlag(mode)) return;
            if (mode == SpecialRenderMode.Shadow && !IsCastingShadows) return;

            if (_distanceFromSqr != 0f || _distanceToSqr != 0f) {
                var distance = (BoundingBox?.GetCenter() - camera.Position)?.LengthSquared();
                if (distance < _distanceFromSqr || distance > _distanceToSqr) return;
            }

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

        private Vector3 GetPosition(InputLayouts.VerticePNTGW4B vin) {
            var weight0 = vin.BonesWeights.X;
            var weight1 = vin.BonesWeights.Y;
            var weight2 = vin.BonesWeights.Z;
            var weight3 = 1.0f - (weight0 + weight1 + weight2);

            var bone0 = _bones[(int)vin.BonesIndices.X];
            var bone1 = _bones[(int)vin.BonesIndices.Y];
            var bone2 = _bones[(int)vin.BonesIndices.Z];
            var bone3 = _bones[(int)vin.BonesIndices.W];

            var s = vin.Position;
            var p = weight0 * Vector3.TransformCoordinate(s, bone0);
            p += weight1 * Vector3.TransformCoordinate(s, bone1);
            p += weight2 * Vector3.TransformCoordinate(s, bone2);
            p += weight3 * Vector3.TransformCoordinate(s, bone3);

            return p;
        }

        public override float? CheckIntersection(Ray ray) {
            try {
                var min = float.MaxValue;
                var found = false;

                var indices = Indices;
                var vertices = Vertices;
                var matrix = ParentMatrix;
                for (int i = 0, n = indices.Length / 3; i < n; i++) {
                    var v0 = Vector3.TransformCoordinate(GetPosition(vertices[indices[i * 3]]), matrix);
                    var v1 = Vector3.TransformCoordinate(GetPosition(vertices[indices[i * 3 + 1]]), matrix);
                    var v2 = Vector3.TransformCoordinate(GetPosition(vertices[indices[i * 3 + 2]]), matrix);

                    float distance;
                    if (!Ray.Intersects(ray, v0, v1, v2, out distance)) continue;
                    if (distance >= min) continue;
                    min = distance;
                    found = true;
                }

                return found ? min : (float?)null;
            } catch (Exception) {
                // mostly for rare, but possible out-of-bounds exception
                return null;
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            DisposeHelper.Dispose(ref _debugMaterial);
            base.Dispose();
        }

        public override BaseRenderableObject Clone() {
            return new ClonedKn5RenderableObject(this);
        }

        internal class ClonedKn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTGW4B> {
            private readonly Kn5SkinnedObject _original;

            internal ClonedKn5RenderableObject(Kn5SkinnedObject original) : base(original.Name + "_copy", original.Vertices, original.Indices) {
                _original = original;
            }

            public override bool IsEnabled => _original.IsEnabled;

            public override bool IsReflectable => _original.IsReflectable;

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (!(_original._isTransparent ? Kn5RenderableObject.TransparentModes : Kn5RenderableObject.OpaqueModes).HasFlag(mode)) return;
                if (mode == SpecialRenderMode.Shadow && !_original.IsCastingShadows || _original._material == null) return;

                if (_original._distanceFromSqr != 0f || _original._distanceToSqr != 0f) {
                    var distance = (BoundingBox?.GetCenter() - camera.Position)?.LengthSquared();
                    if (distance < _original._distanceFromSqr || distance > _original._distanceToSqr) return;
                }

                var material = _original.Material;
                if (!material.Prepare(contextHolder, mode)) return;

                base.DrawOverride(contextHolder, camera, mode);

                if (_original.Emissive.HasValue) {
                    (material as IEmissiveMaterial)?.SetEmissiveNext(_original.Emissive.Value);
                }

                material.SetBones(_original._bones);
                material.SetMatrices(ParentMatrix, camera);
                material.Draw(contextHolder, Indices.Length, mode);
            }

            public override BaseRenderableObject Clone() {
                return new ClonedKn5RenderableObject(_original);
            }
        }
    }
}