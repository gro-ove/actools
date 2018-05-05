using System;
using System.Collections.Generic;
using System.Drawing;
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

        public Color Color => IsEmpty ? Color.White : Vertices.First().Color.ToDrawingColor();

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
            _material.EnsureInitialized(contextHolder);
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

            if (!Ray.Intersects(ray, plane, out var distance)) return false;

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
            var bb = BoundingBox;
            var intersects = bb.HasValue &&
                    Ray.Intersects(pickingRay, bb.Value, out _) &&
                    DoesIntersect(pickingRay, 0.01f / bb.Value.GetCenter().GetOnScreenSize(camera));

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
            for (var i = 0; i < box.Vertices.Length; i++) {
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
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            vertices.Add(new InputLayouts.VerticePC(direction + (left + up - direction) * 0.1f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (-left + up - direction) * 0.1f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (-left - up - direction) * 0.1f, color));
            vertices.Add(new InputLayouts.VerticePC(direction + (left - up - direction) * 0.1f, color));
            indices.AddRange(new ushort[] { 1, 2, 1, 3, 1, 4, 1, 5 });
            indices.AddRange(new ushort[] { 2, 3, 3, 4, 4, 5, 5, 2 });

            return new DebugLinesObject(Matrix.Scaling(new Vector3(size)) * matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesCircle(Matrix matrix, Vector3 direction, Color4 color, int segments = 100, float radius = 0.16f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            left *= radius;
            up *= radius;

            for (var i = 1; i <= segments; i++) {
                var a = MathF.PI * 2f * i / segments;
                vertices.Add(new InputLayouts.VerticePC(left * a.Cos() + up * a.Sin(), color));
                indices.Add((ushort)(i - 1));
                indices.Add((ushort)(i == segments ? 0 : i));
            }

            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesSphere(Matrix matrix, Vector3 direction, Color4 color, int segments = 100, float radius = 0.16f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            var forward = direction * radius;
            left *= radius;
            up *= radius;

            for (var i = 1; i <= segments; i++) {
                var a = MathF.PI * 2f * i / segments;
                var cos = a.Cos();
                var sin = a.Sin();
                vertices.Add(new InputLayouts.VerticePC(left * cos + up * sin, color));
                vertices.Add(new InputLayouts.VerticePC(forward * cos + up * sin, color));
                vertices.Add(new InputLayouts.VerticePC(left * cos + forward * sin, color));
                indices.Add((ushort)(i * 3 - 3));
                indices.Add((ushort)(i == segments ? 0 : i * 3));
                indices.Add((ushort)(i * 3 - 2));
                indices.Add((ushort)(i == segments ? 1 : i * 3 + 1));
                indices.Add((ushort)(i * 3 - 1));
                indices.Add((ushort)(i == segments ? 2 : i * 3 + 2));
            }

            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesRoundedCylinder(Matrix matrix, Vector3 direction, Color4 color, int segments = 100, float radius = 0.16f,
                float height = 0.32f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            var side = direction * height * 0.5f;

            var forward = direction * radius;
            left *= radius;
            up *= radius;

            for (var i = 1; i <= segments; i++) {
                var a = MathF.PI * 2f * i / segments;
                var cos = a.Cos();
                var sin = a.Sin();
                var j = vertices.Count;

                var sa = Math.Abs(cos) < 0.001f;
                var sb = i == segments || i == segments / 2;

                vertices.Add(new InputLayouts.VerticePC(left * cos + up * sin + side, color));
                vertices.Add(new InputLayouts.VerticePC(left * cos + up * sin - side, color));
                vertices.Add(new InputLayouts.VerticePC(forward * cos + up * sin + (cos > 0 && !sa ? side : -side), color));
                vertices.Add(new InputLayouts.VerticePC(left * cos + forward * sin + (sin > 0 && !sb ? side : -side), color));

                var jo = 4;

                if (sa) {
                    var v = new InputLayouts.VerticePC(forward * cos + up * sin + side, color);
                    if (i > segments / 2) {
                        vertices.Add(v);
                    } else {
                        vertices.Add(vertices[j + 2]);
                        vertices[j + 2] = v;
                    }
                    jo++;
                }

                if (sb) {
                    var v = new InputLayouts.VerticePC(left * cos + forward * sin + side, color);
                    if (i > segments / 2) {
                        vertices.Add(v);
                    } else {
                        vertices.Add(vertices[j + 3]);
                        vertices[j + 3] = v;
                    }
                    jo++;
                }

                // circles
                indices.Add((ushort)j);
                indices.Add((ushort)(i == segments ? 0 : j + jo));
                indices.Add((ushort)(j + 1));
                indices.Add((ushort)(i == segments ? 1 : j + jo + 1));

                // first goes-around thing
                indices.Add((ushort)(j + 2));
                if (sa) {
                    indices.Add((ushort)(j + 4));
                    indices.Add((ushort)(j + 4));
                }
                indices.Add((ushort)(i == segments ? 2 : j + jo + 2));

                // second goes-around thing
                indices.Add((ushort)(j + 3));
                if (sb) {
                    indices.Add((ushort)(j + jo - 1));
                    indices.Add((ushort)(j + jo - 1));
                }
                indices.Add((ushort)(i == segments ? 3 : j + jo + 3));
            }

            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesCylinder(Matrix matrix, Vector3 direction, Color4 color, int segments = 100, float radius = 0.16f,
                float height = 0.32f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            var side = direction * height * 0.5f;
            left *= radius;
            up *= radius;

            vertices.Add(new InputLayouts.VerticePC(left + up + side, color));
            vertices.Add(new InputLayouts.VerticePC(left + up - side, color));
            vertices.Add(new InputLayouts.VerticePC(left - up + side, color));
            vertices.Add(new InputLayouts.VerticePC(left - up - side, color));
            vertices.Add(new InputLayouts.VerticePC(-left + up + side, color));
            vertices.Add(new InputLayouts.VerticePC(-left + up - side, color));
            vertices.Add(new InputLayouts.VerticePC(-left - up + side, color));
            vertices.Add(new InputLayouts.VerticePC(-left - up - side, color));
            indices.AddRange(Enumerable.Range(0, 8).Select(x => (ushort)x));

            for (var i = 1; i <= segments; i++) {
                var a = MathF.PI * 2f * i / segments;
                var cos = a.Cos();
                var sin = a.Sin();
                var j = vertices.Count;

                vertices.Add(new InputLayouts.VerticePC(left * cos + up * sin + side, color));
                vertices.Add(new InputLayouts.VerticePC(left * cos + up * sin - side, color));

                // circles
                indices.Add((ushort)j);
                indices.Add((ushort)(i == segments ? 0 : j + 2));
                indices.Add((ushort)(j + 1));
                indices.Add((ushort)(i == segments ? 1 : j + 3));
            }

            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        [NotNull]
        public static DebugLinesObject GetLinesPlane(Matrix matrix, Vector3 direction, Color4 color, float width = 0.16f, float length = 0.16f) {
            var vertices = new List<InputLayouts.VerticePC>();
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.9f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            up *= length / 2f;
            left *= width / 2f;

            for (var i = 1; i <= 4; i++) {
                var l = i == 2 || i == 3 ? 1f : -1f;
                var f = i == 1 || i == 2 ? 1f : -1f;
                vertices.Add(new InputLayouts.VerticePC(left * l + up * f, color));
                indices.Add((ushort)(i - 1));
                indices.Add((ushort)(i == 4 ? 0 : i));
            }

            return new DebugLinesObject(matrix, vertices.ToArray(), indices.ToArray());
        }

        public static DebugLinesObject GetLinesCone(float angle, Vector3 direction, Color4 color, int segments = 8, int subSegments = 10, float size = 0.1f) {
            var vertices = new List<InputLayouts.VerticePC> { new InputLayouts.VerticePC(Vector3.Zero, color) };
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.5f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            var angleSin = angle.Sin();
            var angleCos = angle.Cos();

            left *= size * angleSin;
            up *= size * angleSin;
            direction *= size * angleCos;

            var total = segments * subSegments;
            for (var i = 1; i <= total; i++) {
                var a = MathF.PI * 2f * i / total;
                vertices.Add(new InputLayouts.VerticePC(left * a.Cos() + up * a.Sin() + direction, color));
                indices.Add((ushort)i);
                indices.Add((ushort)(i == total ? 1 : i + 1));

                if (i % subSegments == 1) {
                    indices.Add(0);
                    indices.Add((ushort)i);
                }
            }

            return new DebugLinesObject(Matrix.Identity, vertices.ToArray(), indices.ToArray());
        }
    }
}