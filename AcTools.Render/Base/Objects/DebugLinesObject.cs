using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public class DebugLinesObject : LinesRenderableObject<InputLayouts.VerticePC> {
        public DebugLinesObject(Matrix transform, InputLayouts.VerticePC[] vertices, ushort[] indices) : base(null, vertices, indices) {
            Transform = transform;
        }

        public DebugLinesObject(Matrix transform, InputLayouts.VerticePC[] vertices) : base(null, vertices) {
            Transform = transform;
        }

        public Matrix Transform;
        private IRenderableMaterial _material;

        public override void UpdateBoundingBox() {
            var matrix = Transform * ParentMatrix;
            BoundingBox = IsEmpty ? (BoundingBox?)null : Vertices.Select(x => Vector3.TransformCoordinate(x.Position, matrix)).ToBoundingBox();
        }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = contextHolder.GetMaterial(BasicMaterials.DebugLinesKey);
            _material.Initialize(contextHolder);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_material.Prepare(contextHolder, mode)) return;
            base.DrawOverride(contextHolder, camera, mode);

            _material.SetMatrices(Transform * ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public bool DrawHighlighted(Ray pickingRay, IDeviceContextHolder contextHolder, ICamera camera) {
            float distance;
            var intersects = Ray.Intersects(pickingRay, BoundingBox ?? default(BoundingBox), out distance);
            Draw(contextHolder, camera, intersects ? SpecialRenderMode.Outline : SpecialRenderMode.Simple);
            return intersects;
        }

        public override void Dispose() {
            base.Dispose();
            _material?.Dispose();
        }
    }

    public class DebugObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
        public DebugObject(Matrix transform, InputLayouts.VerticePNTG[] vertices, ushort[] indices) : base(null, vertices, indices) {
            Transform = transform;
        }

        public DebugObject(Matrix transform, InputLayouts.VerticePNTG[] vertices) : base(null, vertices, GetIndices(vertices.Length)) {
            Transform = transform;
        }

        public DebugObject(Matrix transform, GeometryGenerator.MeshData meshData) : base(null, GetVertices(meshData), GetIndices(meshData)) {
            Transform = transform;
        }

        private static InputLayouts.VerticePNTG[] GetVertices(GeometryGenerator.MeshData data) {
            var result = new InputLayouts.VerticePNTG[data.Vertices.Count];
            for (var i = data.Vertices.Count - 1; i >= 0; i--) {
                var v = data.Vertices[i];
                result[i] = new InputLayouts.VerticePNTG(v.Position, v.Normal, v.TexC, v.TangentU);
            }
            return result;
        }

        private static ushort[] GetIndices(GeometryGenerator.MeshData data) {
            var result = new ushort[data.Indices.Count];
            for (var i = data.Indices.Count - 1; i >= 0; i--) {
                result[i] = (ushort)data.Indices[i];
            }
            return result;
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
            _material.Initialize(contextHolder);
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