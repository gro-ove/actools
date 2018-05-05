using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class DebugObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
        public DebugObject(Matrix transform, InputLayouts.VerticePNTG[] vertices, ushort[] indices) : base(null, vertices, indices) {
            Transform = transform;
        }

        public DebugObject(Matrix transform, InputLayouts.VerticePNTG[] vertices) : base(null, vertices, GetIndices(vertices.Length)) {
            Transform = transform;
        }

        public DebugObject(Matrix transform, GeometryGenerator.MeshData meshData) : base(null, InputLayouts.VerticePNTG.Convert(meshData.Vertices), meshData.Indices) {
            Transform = transform;
        }

        private static ushort[] GetIndices(int verticesCount) {
            var result = new ushort[verticesCount];
            for (var i = 0; i < result.Length; i++) {
                result[i] = (ushort)i;
            }
            return result;
        }

        public Matrix Transform;
        private IRenderableMaterial _material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = new Kn5MaterialSimpleGl();
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
    }
}