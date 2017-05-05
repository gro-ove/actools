using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using JetBrains.Annotations;
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
            BoundingBox = IsEmpty ? (BoundingBox?)null : Vertices.Select(x => Vector3.TransformCoordinate(x.Position, matrix))
                                                                 .ToBoundingBox().Grow(new Vector3(0.05f));
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

        private bool DoesRayIntersectLineSegment(Ray ray, Vector3 a, Vector3 b, float lineWidth) {
            var segmentN = Vector3.Normalize(b - a);
            if (segmentN == ray.Direction) return false;

            a -= segmentN * lineWidth;
            b += segmentN * lineWidth;

            var planeD = Vector3.Normalize(Vector3.Cross(ray.Direction, segmentN));
            var planeN = Vector3.Normalize(Vector3.Cross(segmentN, planeD));
            var plane = new Plane(a, planeN);

            float distance;
            if (!Ray.Intersects(ray, plane, out distance)) return false;

            var point = ray.Position + ray.Direction * distance;
            if (Vector3.Dot(Vector3.Normalize(point - b), -segmentN) < 0) return false;

            var pointNProj = Vector3.Dot(Vector3.Normalize(point - a), segmentN);
            if (pointNProj < 0) return false;

            var proj = (point - a).Length() * pointNProj * segmentN;
            distance = (a + proj - point).Length();
            return distance < lineWidth;
        }

        public bool DoesIntersect(Ray ray, float lineWidth) {
            var matrix = Transform * ParentMatrix;
            for (var i = 1; i < Indices.Length; i++) {
                var j0 = Indices[i - 1];
                var j1 = Indices[i];
                var v0 = Vector3.TransformCoordinate(Vertices[j0].Position, matrix);
                var v1 = Vector3.TransformCoordinate(Vertices[j1].Position, matrix);
                if (DoesRayIntersectLineSegment(ray, v0, v1, lineWidth)) return true;
            }

            return false;
        }

        public bool DrawHighlighted(Ray pickingRay, IDeviceContextHolder contextHolder, ICamera camera) {
            float distance;
            var intersects = Ray.Intersects(pickingRay, BoundingBox ?? default(BoundingBox), out distance);
            if (intersects) {
                intersects = DoesIntersect(pickingRay, 0.02f);
            }

            Draw(contextHolder, camera, intersects ? SpecialRenderMode.Outline : SpecialRenderMode.Simple);
            return intersects;
        }

        public override void Dispose() {
            base.Dispose();
            _material?.Dispose();
        }

        [NotNull]
        public static DebugLinesObject GetLinesBox(Matrix matrix, Vector3 size, Color4 color) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            var box = GeometryGenerator.CreateLinesBox(size);
            for (var i = 0; i < box.Vertices.Count; i++) {
                vertices.Add(new InputLayouts.VerticePC(box.Vertices[i].Position, color));
            }

            indices.AddRange(box.Indices);
            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesArrow(Matrix matrix, Vector3 direction, Color4 color, float size = 0.2f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            vertices.Add(new InputLayouts.VerticePC(new Vector3(0f), color));
            vertices.Add(new InputLayouts.VerticePC(direction, color));
            indices.AddRange(new ushort[] { 0, 1 });

            Vector3 left, up;
            if (direction == Vector3.UnitY) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            vertices.Add(new InputLayouts.VerticePC(direction + (left + up - direction) * 0.2f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (-left + up - direction) * 0.2f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (-left - up - direction) * 0.2f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (left - up - direction) * 0.2f, color));
            indices.AddRange(new ushort[] { 1, 2, 1, 3, 1, 4, 1, 5 });
            indices.AddRange(new ushort[] { 2, 3, 3, 4, 4, 5, 5, 2 });

            return new DebugLinesObject(Matrix.Scaling(new Vector3(size)) * matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesCircle(Matrix matrix, Vector3 direction, Color4 color, int segments = 100, float size = 0.16f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (direction == Vector3.UnitY) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            for (var i = 1; i <= segments; i++) {
                var a = MathF.PI * 2f * i / segments;
                vertices.Add(new InputLayouts.VerticePC(left * a.Cos() + up * a.Sin(), color));
                indices.Add((ushort)(i - 1));
                indices.Add((ushort)(i == segments ? 0 : i));
            }

            return new DebugLinesObject(Matrix.Scaling(new Vector3(size)) * matrix, vertices.ToArray(), indices.ToArray());
        }
    }
}