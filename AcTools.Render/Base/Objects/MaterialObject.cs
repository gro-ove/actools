using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class MaterialObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
        private readonly SpecialRenderMode _renderModes;

        public MaterialObject(Matrix transform, InputLayouts.VerticePNTG[] vertices, ushort[] indices, bool isTransparent)
                : base(null, vertices, indices) {
            Transform = transform;
            _renderModes = isTransparent ? TransparentModes : OpaqueModes;
        }

        public MaterialObject(Matrix transform, GeometryGenerator.MeshData data, bool isTransparent)
                : this(transform, InputLayouts.VerticePNTG.Convert(data.Vertices), data.Indices, isTransparent) { }

        public Matrix Transform;
        public bool IsCastingShadows;

        [CanBeNull]
        public object MaterialKey;

        [CanBeNull]
        public IRenderableMaterial Material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            if (Material == null && MaterialKey != null) {
                Material = contextHolder.Get<SharedMaterials>().GetMaterial(MaterialKey);
            }

            Material?.EnsureInitialized(contextHolder);
        }

        // TODO: Move outside, unite with Kn5RenderableObject values?
        internal static readonly SpecialRenderMode TransparentModes = SpecialRenderMode.SimpleTransparent |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.DeferredTransparentForw | SpecialRenderMode.DeferredTransparentDef | SpecialRenderMode.DeferredTransparentMask;

        internal static readonly SpecialRenderMode OpaqueModes = SpecialRenderMode.Simple |
                SpecialRenderMode.Outline | SpecialRenderMode.GBuffer |
                SpecialRenderMode.Deferred | SpecialRenderMode.Reflection | SpecialRenderMode.Shadow;

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if ((_renderModes & mode) == 0) return;
            if (mode == SpecialRenderMode.Shadow && !IsCastingShadows) return;

            var material = Material;
            if (material?.Prepare(contextHolder, mode) != true) return;

            base.DrawOverride(contextHolder, camera, mode);
            material.SetMatrices(Transform * ParentMatrix, camera);
            material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref Material);
            base.Dispose();
        }
    }
}