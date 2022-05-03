using System;
using System.Collections.Generic;
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
    public sealed class Kn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG>, IKn5RenderableObject {
        public readonly bool IsCastingShadows;

        public Kn5Node OriginalNode { get; }

        public Matrix ModelMatrixInverted { get; set; }

        private SpecialRenderMode _renderModes;
        private bool _isTransparent;

        private bool IsTransparent {
            get => _isTransparent;
            set {
                _isTransparent = value;
                _renderModes = value ? TransparentModes : OpaqueModes;
            }
        }

        private readonly float _distanceFromSqr, _distanceToSqr;
        private readonly bool _distanceLimit;

        public Kn5RenderableObject(Kn5Node node) : base(node.Name, InputLayouts.VerticePNTG.Convert(node.Vertices), node.Indices.ToIndicesFixX()) {
            OriginalNode = node;
            IsCastingShadows = node.CastShadows;

            if (IsEnabled && (!OriginalNode.Active || !OriginalNode.IsVisible || !OriginalNode.IsRenderable)) {
                IsEnabled = false;
            }

            if (OriginalNode.IsTransparent || OriginalNode.Layer == 1 /* WHAT? WHAT DOES IT DO? BUT KUNOS PREVIEWS SHOWROOM WORKS THIS WAY, SO… */) {
                IsReflectable = false;
            }

            IsTransparent = OriginalNode.IsTransparent;

            _distanceLimit = OriginalNode.LodIn != 0f || OriginalNode.LodOut != 0f;
            if (_distanceLimit) {
                _distanceFromSqr = OriginalNode.LodIn.Pow(2f);
                _distanceToSqr = OriginalNode.LodOut.Pow(2f);
            }
        }

        public void SetCustomMaterial(IRenderableMaterial material) {
            _material = material;
        }

        private IRenderableMaterial Material => _debugMaterial ?? _mirrorMaterial ?? _material;

        [CanBeNull]
        private IRenderableMaterial _mirrorMaterial;

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) {
            if (enabled == (_mirrorMaterial != null)) return;

            if (enabled) {
                _mirrorMaterial = holder.GetMaterial(BasicMaterials.MirrorKey);
                if (IsInitialized) {
                    _mirrorMaterial.EnsureInitialized(holder);
                }
            } else {
                DisposeHelper.Dispose(ref _mirrorMaterial);
            }
        }

        [CanBeNull]
        private IRenderableMaterial _debugMaterial;

        public void SetDebugMode(IDeviceContextHolder holder, bool enabled) {
            if (enabled == (_debugMaterial != null)) return;

            if (enabled) {
                _debugMaterial = holder.Get<SharedMaterials>().GetMaterial(new Tuple<object, uint>(BasicMaterials.DebugKey, OriginalNode.MaterialId));
                if (_debugMaterial == null) return;
                if (IsInitialized) {
                    _debugMaterial.EnsureInitialized(holder);
                }
            } else {
                DisposeHelper.Dispose(ref _debugMaterial);
            }
        }

        int IKn5RenderableObject.TrianglesCount => GetTrianglesCount();

        public void SetTransparent(bool? isTransparent) {
            IsTransparent = isTransparent ?? OriginalNode.IsTransparent;
        }

        [CanBeNull]
        private IRenderableMaterial _material;

        public override IEnumerable<int> GetMaterialIds() {
            return new []{ (int)OriginalNode.MaterialId };
        }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            if (_material == null) {
                _material = contextHolder.Get<SharedMaterials>().GetMaterial(OriginalNode.MaterialId);
                _material.EnsureInitialized(contextHolder);
            }

            _mirrorMaterial?.EnsureInitialized(contextHolder);
            _debugMaterial?.EnsureInitialized(contextHolder);

            var node = OriginalNode;
            if (node.Name.EndsWith(" Light") && !node.IsTransparent && !node.CastShadows && Material.IsBlending) {
                // shameless fix for some of the cars I made 🙃
                _renderModes &= ~SpecialRenderMode.GBuffer;
            }
        }

        internal static readonly SpecialRenderMode TransparentModes = SpecialRenderMode.SimpleTransparent |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.DeferredTransparentForw | SpecialRenderMode.DeferredTransparentDef | SpecialRenderMode.DeferredTransparentMask;

        internal static readonly SpecialRenderMode OpaqueModes = SpecialRenderMode.Simple |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.Deferred | SpecialRenderMode.Reflection | SpecialRenderMode.Shadow;

        public AcDynamicMaterialParams DynamicMaterialParams { get; } = new AcDynamicMaterialParams();

        private void DrawOverrideBase(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            base.DrawOverride(contextHolder, camera, mode);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if ((_renderModes & mode) == 0) return;
            if (mode == SpecialRenderMode.Shadow && !IsCastingShadows) return;

            if (_distanceLimit) {
                var bb = BoundingBox;
                if (bb == null) return;

                var distance = (bb.Value.GetCenter() - camera.Position).LengthSquared();
                if (distance < _distanceFromSqr || distance > _distanceToSqr) return;
            }

            var material = Material;
            if (!material.Prepare(contextHolder, mode)) return;

            base.DrawOverride(contextHolder, camera, mode);
            DynamicMaterialParams.SetMaterial(contextHolder, material as IAcDynamicMaterial);

            material.SetMatrices(ParentMatrix, camera);
            material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            DisposeHelper.Dispose(ref _mirrorMaterial);
            DisposeHelper.Dispose(ref _debugMaterial);
            base.Dispose();
        }

        public override BaseRenderableObject Clone() {
            return new ClonedKn5RenderableObject(this);
        }

        private class ClonedKn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
            private readonly Kn5RenderableObject _original;

            internal ClonedKn5RenderableObject(Kn5RenderableObject original) : base(original.Name + "_copy", original.Vertices, original.Indices) {
                _original = original;
                CloneFrom(_original);
            }

            public override bool IsEnabled => _original.IsEnabled;
            public override bool IsReflectable => _original.IsReflectable;

            protected override void Initialize(IDeviceContextHolder contextHolder) {}

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if ((_original._renderModes & mode) == 0) return;
                if (mode == SpecialRenderMode.Shadow && !_original.IsCastingShadows || _original._material == null) return;

                if (_original._distanceFromSqr != 0f || _original._distanceToSqr != 0f) {
                    var distance = (BoundingBox?.GetCenter() - camera.Position)?.LengthSquared();
                    if (distance < _original._distanceFromSqr || distance > _original._distanceToSqr) return;
                }

                var material = _original.Material;
                if (!material.Prepare(contextHolder, mode)) return;

                _original.DrawOverrideBase(contextHolder, camera, mode);
                _original.DynamicMaterialParams.SetMaterial(contextHolder, material as IAcDynamicMaterial);

                material.SetMatrices(ParentMatrix, camera);
                material.Draw(contextHolder, Indices.Length, mode);
            }

            public override BaseRenderableObject Clone() {
                return new ClonedKn5RenderableObject(_original);
            }
        }
    }
}
