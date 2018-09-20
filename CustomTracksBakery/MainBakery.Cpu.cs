/*using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace CustomTracksBakery {
    public partial class MainBakery {
        private class SurfaceTriangle {
            public Vector3 v0, v1, v2;
            public Vector3 t0, t1, t2;
            public float w0m0, w0m1, w1m0, w1m1;

            public SurfaceTriangle(InputLayouts.VerticePNTG[] vertices, ushort[] indices, int index) {
                v0 = vertices[indices[index]].Position;
                v1 = vertices[indices[index + 1]].Position;
                v2 = vertices[indices[index + 2]].Position;
                // if (!Ray.Intersects(ray, v0, v1, v2, out distance) || distance > maxDistance) continue;

                t0 = vertices[indices[index]].Tangent;
                t1 = vertices[indices[index + 1]].Tangent;
                t2 = vertices[indices[index + 2]].Tangent;

                var mult = 1.0f / ((v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z));
                w0m0 = (v1.Z - v2.Z) * mult;
                w0m1 = (v2.X - v1.X) * mult;
                w1m0 = (v2.Z - v0.Z) * mult;
                w1m1 = (v0.X - v2.X) * mult;
            }

            public bool GetW(float x, float y, out float w0, out float w1, out float w2) {
                w0 = w0m0 * (x - v2.X) + w0m1 * (y - v2.Z);
                w1 = w1m0 * (x - v2.X) + w1m1 * (y - v2.Z);
                w2 = 1.0f - w0 - w1;
                return 0 <= w0 && w0 <= 1 && 0 <= w1 && w1 <= 1 && 0 <= w2 && w2 <= 1;
            }
        }

        private class Surface {
            public BakedObject Baked;
            public BoundingBox Box;
            public SurfaceTriangle[] Triangles;

            public static Surface Create(BakedObject o) {
                var result = new Surface { Baked = o };

                var obj = o;
                var indices = obj.Indices;
                var vertices = obj.Vertices;

                var triangles = new List<SurfaceTriangle>();
                var faulty = 0;
                for (int i = 0, n = indices.Length / 3; i < n; i++) {
                    var n0 = vertices[indices[i * 3]].Normal;
                    var n1 = vertices[indices[i * 3 + 1]].Normal;
                    var n2 = vertices[indices[i * 3 + 2]].Normal;
                    if ((n0.Y < 0.0f || n1.Y <= 0.0f || n2.Y < 0.0f) && ++faulty > 10) {
                        Trace.WriteLine("Miss: " + n0 + "; " + n1 + "; " + n2);
                        return null;
                    }

                    triangles.Add(new SurfaceTriangle(vertices, indices, i * 3));

                    var v0 = vertices[indices[i * 3]].Position;
                    var v1 = vertices[indices[i * 3 + 1]].Position;
                    var v2 = vertices[indices[i * 3 + 2]].Position;
                    SlimDxExtension.Extend(ref result.Box, ref v0);
                    SlimDxExtension.Extend(ref result.Box, ref v1);
                    SlimDxExtension.Extend(ref result.Box, ref v2);
                }

                result.Triangles = triangles.ToArray();
                return result;
            }

            private Surface() { }
        }

        private static Vector3? CheckIntersection(Surface s, Ray ray, float maxDistance) {
            // Trace.WriteLine("Surface: " + s.Baked.Object.OriginalNode.Name + ", BB: " + s.Box + ", pos: " + ray.Position);
            for (int i = 0, n = s.Triangles.Length; i < n; i++) {
                if (s.Triangles[i].GetW(ray.Position.X, ray.Position.Z, out var w0, out var w1, out var w2)) {
                    Trace.WriteLine($"Found surface: {w0:F3}, {w1:F3}, {w2:F3}");

                    var t = s.Triangles[i];
                    if (s.Baked.Mode == BakedMode.TangentLength) {
                        var a0 = t.t0.Length() * 2.0f - 1.0f;
                        var a1 = t.t1.Length() * 2.0f - 1.0f;
                        var a2 = t.t2.Length() * 2.0f - 1.0f;
                        return new Vector3(a0 * w0 + a1 * w1 + a2 * w2);
                    } else {
                        var a0 = t.t0 / 1e5f + new Vector3(1.0f);
                        var a1 = t.t1 / 1e5f + new Vector3(1.0f);
                        var a2 = t.t2 / 1e5f + new Vector3(1.0f);
                        return a0 * w0 + a1 * w1 + a2 * w2;
                    }
                }
            }

            return null;
        }
    }

    public partial class MainBakery {
        private void SyncGrassCpu() {
            var grass = _nodesToBake.Where(x => x.IsGrass).Where(x => x.Object.OriginalNode.Name.Contains("_HI_")).ToList();
            // Trace.WriteLine(grass.Select(x => x.Object.OriginalNode.Name).JoinToString("; "));
            Trace.WriteLine($"Syncing grass with underlying surfaces: {grass.Count} {(grass.Count == 1 ? "mesh" : "meshes")}");

            var s = Stopwatch.StartNew();
            var surfaces = new[] { _mainNode }.Concat(_includeNodeFiles).Concat(_occluderNodeFiles)
                                              .SelectMany(file => FlattenFile(_filters, file))
                                              .Where(x => !x.IsGrass && !x.IsTree && x.GetMaterial()?.AlphaTested == false)
                                              .Where(x => x.GetMaterialName() == "grass")
                                              .Select(Surface.Create).NonNull().ToArray();
            var bb = surfaces.Select(x => x.Box).ToArray();
            // Trace.WriteLine(surfaces.Select(x => x.Baked.GetMaterialName()).Distinct().JoinToString("; "));
            Trace.WriteLine($"Occluders preparation: {s.Elapsed.TotalMilliseconds:F1} ms (found {_occluderNodes.Length} occluders)");
            s.Restart();

            var up = Vector3.UnitY * 0.2f;
            var down = -Vector3.UnitY;

            for (var grassIndex = 0; grassIndex < grass.Count; grassIndex++) {
                var o = grass[grassIndex];
                for (var vertexIndex = 0; vertexIndex < o.Object.Vertices.Length; vertexIndex++) {
                    var vertex = o.Object.Vertices[vertexIndex];
                    var pos = vertex.Position;
                    var ray = new Ray(pos + up, down);
                    Vector3 found = -Vector3.UnitY;
                    for (var i = 0; i < bb.Length; i++) {
                        var b = bb[i];
                        if (b.Minimum.X < pos.X && b.Maximum.X > pos.X
                                && b.Minimum.Z < pos.Z && b.Maximum.Z > pos.Z
                                && b.Minimum.Y < pos.Y && b.Maximum.Y + 0.5f > pos.Y) {
                            var n = surfaces[i];
                            var inter = CheckIntersection(n, ray, 0.5f);
                            if (inter.HasValue) {
                                found = inter.Value;
                                SetVector(o.Object.OriginalNode.Vertices[vertexIndex].TangentU, inter.Value);
                                // Trace.WriteLine("Hit: " + n.Baked.Object.OriginalNode.Name + ", BB: " + n.Box + ", pos: " + ray.Position + ", mat.: " + n.Baked.GetMaterialName());
                            } else {
                                // Trace.WriteLine("Miss: " + n.Baked.Object.OriginalNode.Name + ", BB: " + n.Box + ", pos: " + ray.Position + ", mat.: " + n.Baked.GetMaterialName());
                            }
                        }
                    }

                    if (found != -Vector3.UnitY) { } else {
                        Trace.WriteLine("Nothing found");
                        // Environment.Exit(1);
                    }
                }

                Trace.WriteLine($"Grass mesh synced: {o.Object.OriginalNode.Name} ({grassIndex}/{grass.Count})");
            }

            Trace.WriteLine($"Grass meshes synced: {s.Elapsed.ToReadableTime()}");
        }
    }
}*/