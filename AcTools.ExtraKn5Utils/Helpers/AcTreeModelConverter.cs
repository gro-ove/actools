using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.FbxUtils;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using xxHashSharp;

namespace AcTools.ExtraKn5Utils.Helpers {
    public class AcTreeModelConverter {
        class MeshBuilder {
            public string Name { get; private set; }

            public MeshBuilder(string name, int index) {
                Name = index == 0 ? name : name + "_SUB" + index;
            }

            public void SetSub() {
                Name = Name + "_SUB0";
            }

            public readonly List<ushort> Indices = new List<ushort>();
            private readonly List<Kn5Node.Vertex> _vertices = new List<Kn5Node.Vertex>();
            private readonly Dictionary<Kn5Node.Vertex, int> _knownVertices = new Dictionary<Kn5Node.Vertex, int>();

            public void AddVertex(Vec3 pos, Vec3 normal, Vec2 uv) {
                var vertex = new Kn5Node.Vertex(pos, normal, uv, new Vec3());
                if (_knownVertices.TryGetValue(vertex, out var r)) {
                    Indices.Add((ushort)r);
                    return;
                }
                r = _vertices.Count;
                _knownVertices[vertex] = r;
                _vertices.Add(vertex);
                Indices.Add((ushort)r);
            }

            public void Write(ExtendedBinaryWriter writer) {
                var mesh = new Kn5Node {
                    NodeClass = Kn5NodeClass.Mesh,
                    Indices = Indices.ToArray(),
                    Vertices = _vertices.ToArray()
                };
                mesh.RecalculateTangents();

                writer.Write(mesh.Indices.Length);
                for (int i = 0; i < mesh.Indices.Length; ++i) {
                    writer.Write(mesh.Indices[i]);
                }

                writer.Write(mesh.Vertices.Length);
                for (int i = 0; i < mesh.Vertices.Length; ++i) {
                    writer.Write(mesh.Vertices[i].Position);
                    writer.Write(mesh.Vertices[i].Normal);
                    writer.Write(mesh.Vertices[i].Tex);

                    // Converted tangent:
                    writer.Write((byte)((mesh.Vertices[i].Tangent.X * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                    writer.Write((byte)((mesh.Vertices[i].Tangent.Y * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                    writer.Write((byte)((mesh.Vertices[i].Tangent.Z * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                    writer.Write((byte)0); // element ID
                    writer.WriteHalf(0f); // UV2.x
                    writer.WriteHalf(0f); // UV2.y
                    writer.Write((byte)255); // AO0
                    writer.Write((byte)255); // AO1
                    writer.Write((byte)255); // wet
                    writer.Write((byte)0); // extra
                }
            }
        }

        public class AABB {
            public Vec3 Min = new Vec3(float.PositiveInfinity);
            public Vec3 Max = new Vec3(float.NegativeInfinity);

            public void Extend(Vec3 v) {
                for (var k = 0; k < 3; ++k) {
                    Min[k] = Math.Min(Min[k], v[k]);
                    Max[k] = Math.Max(Max[k], v[k]);
                }
            }

            public float Finish() {
                var maxDimension = Math.Max(Math.Max(Max.X, Max.Z), Math.Max(-Min.X, -Min.Z));
                Min.X = 0f;
                Min.Z = 0f;
                Max.X = maxDimension;
                Max.Z = maxDimension;
                return maxDimension;
            }

            public float TrunkWidth() {
                return Math.Min(Math.Min(Max.X, Max.Z), Math.Min(-Min.X, -Min.Z));
            }

            public Vec3 Normalize(Vec3 v) {
                return (v - Min) / (Max - Min);
            }
        }

        public static void Convert(string model, string destination) {
            var fbx = FbxIO.Read(model);

            var aabb = ComputeAABB(fbx);
            var lods = new List<Tuple<string, Tuple<AABB, int, List<string>>>>();
            using (var stream = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                foreach (var name in CollectMeshNames(fbx)) {
                    using (var entry = zip.CreateEntry(name + ".bin").Open()) {
                        lods.Add(Tuple.Create(name + ".bin", ConvertModel(fbx, entry, aabb, x => x == name)));
                    }
                }

                lods = lods.OrderByDescending(x => x.Item2.Item2).ToList();
                var maxDistance = lods[0].Item2.Item1.Max.Y * 10f;
                var cfg = new IniFile {
                    ["BASIC"] = { ["HEIGHT"] = lods[0].Item2.Item1.Max.Y, ["WIDTH"] = lods[0].Item2.Item1.Finish() * 2 },
                    ["SHADING"] = { ["SPECULAR"] = "1.0", ["SUBSCATTERING"] = "1.0", ["REFLECTIVITY"] = "1.0" },
                };
                for (var i = 0; i < lods.Count; ++i) {
                    cfg["LOD_" + i]["MODEL"] = lods[i].Item1;
                    cfg["LOD_" + i]["DISTANCE"] = maxDistance * (i + 1) / lods.Count;
                }
                zip.AddString("tree.ini", cfg.ToString());

                foreach (var texture in lods.SelectMany(x => x.Item2.Item3).Distinct()) {
                    zip.AddBytes(Path.GetFileName(texture), File.ReadAllBytes(texture));
                }
            }
        }

        private static uint CalculateChecksum(string filename) {
            var data = File.ReadAllBytes(filename);
            var hash = new xxHash();
            hash.Init();
            hash.Update(data, data.Length);
            var ret = hash.Digest();
            return ret == 0U ? 1U : ret;
        }

        private static IEnumerable<string> CollectMeshNames(FbxDocument fbx) {
            return fbx.GetGeometryIds().Select(x => fbx.GetNode("Model", fbx.GetConnection(x)).GetName("Model"));
        }

        private static AABB ComputeAABB(FbxDocument fbx) {
            var aabb = new AABB();
            foreach (var id in fbx.GetGeometryIds()) {
                var geometry = fbx.GetGeometry(id);
                var modelId = fbx.GetConnection(id);
                var name = fbx.GetNode("Model", modelId).GetName("Model");

                var fbxIndices = geometry?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();
                if (fbxIndices == null) {
                    AcToolsLogging.Write($"Indices data is missing: {name}");
                    continue;
                }

                AcToolsLogging.Write($"Mesh: {name}");

                var fbxVertices = geometry.GetRelative("Vertices").Value.GetAsFloatArray();
                AcToolsLogging.Write($"\tVertices: {fbxVertices.Length / 3d}");
                AcToolsLogging.Write($"\tTriangles: {fbxIndices.Length / 3}");

                for (var i = 0; i < fbxIndices.Length; ++i) {
                    var j = fbxIndices[i];
                    if (j < 0) {
                        j = -j - 1;
                    }

                    aabb.Extend(new Vec3(fbxVertices[j * 3], fbxVertices[j * 3 + 2], fbxVertices[j * 3 + 1]));
                }
            }
            aabb.Finish();
            return aabb;
        }

        private static Tuple<AABB, int, List<string>> ConvertModel(FbxDocument fbx, Stream output, AABB aabb, Func<string, bool> filter) {
            using (var writer = new ExtendedBinaryWriter(output, true)) {
                var textures = fbx.GetFbxNodes("Texture", fbx).Select(x => new {
                    Key = x.Value.GetAsLong(),
                    Texture = x.GetRelative("FileName").Value.GetAsString(),
                    Checksum = CalculateChecksum(x.GetRelative("FileName").Value.GetAsString())
                }).ToList();
                var materials = fbx.GetFbxNodes("Material", fbx).Select(x => new {
                    Key = x.Value.GetAsLong(),
                    Name = x.GetName("Material"),
                    Textures = textures.SelectMany(t =>
                            fbx.GetAllConnections(t.Key).Where(f => f.Item1 == x.Value.GetAsLong()).Select(
                                    f => new { Texture = t, Role = f.Item2 })).ToList()
                }).ToList();

                var uniqueTextures = textures.DistinctBy(t => t.Checksum).ToList();
                var texturesMap = uniqueTextures.ToDictionary(t => t.Checksum, t => uniqueTextures.IndexOf(t));
                texturesMap[0U] = -1;

                writer.Write(1);
                writer.Write((byte)0);
                writer.Write(uniqueTextures.Count);
                foreach (var texture in uniqueTextures) {
                    writer.Write(texture.Checksum);
                    writer.Write(Path.GetFileName(texture.Texture));
                }

                var aabbMesh = new AABB();
                var aabbTrunk = new AABB();
                var indicesCount = 0;
                foreach (var id in fbx.GetGeometryIds()) {
                    var geometry = fbx.GetGeometry(id);
                    var modelId = fbx.GetConnection(id);
                    var name = fbx.GetNode("Model", modelId).GetName("Model");
                    if (filter?.Invoke(name) == false) continue;

                    var fbxIndices = geometry?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();

                    if (fbxIndices == null) {
                        AcToolsLogging.Write($"Indices data is missing: {name}");
                        continue;
                    }

                    AcToolsLogging.Write($"Mesh: {name}");

                    var fbxVertices = geometry.GetRelative("Vertices").Value.GetAsFloatArray();
                    AcToolsLogging.Write($"\tVertices: {fbxVertices.Length / 3d}");
                    AcToolsLogging.Write($"\tTriangles: {fbxIndices.Length / 3}");

                    var layerNormal = geometry.GetRelative("LayerElementNormal");
                    var layerMaterial = geometry.GetRelative("LayerElementMaterial");
                    var layerUV = geometry.GetRelative("LayerElementUV");
                    // AcToolsLogging.Write($"\tFound layers: {geometry.Nodes.Select(x => x?.Identifier.Value).NonNull().JoinToString(", ")}");
                    if (layerNormal == null || layerMaterial == null || layerUV == null) {
                        AcToolsLogging.Write($"\tLayer missing (found: {geometry.Nodes.Select(x => x?.Identifier.Value).NonNull().JoinToString(", ")})");
                        continue;
                    }

                    var fbxNormals = new FbxDataAccessor(layerNormal, "Normals");
                    var fbxUV = new FbxDataAccessor(layerUV, "UV");
                    var fbxMaterial = new FbxDataAccessor(layerMaterial, "Materials");

                    var filteredMaterials = materials.Where(x => fbx.GetAllConnections(x.Key).Any(f => f.Item1 == modelId)).ToList();
                    if (filteredMaterials.Count == 0) {
                        throw new Exception("No materials found: " + name);
                    }

                    var dictionary = new Dictionary<float, MeshBuilder>();

                    MeshBuilder GetBuilder(float material) {
                        return dictionary.GetValueOrSet(material, () => {
                            if (dictionary.Count == 1) {
                                dictionary.First().Value.SetSub();
                            }
                            return new MeshBuilder(name, dictionary.Count);
                        });
                    }

                    for (var i = 0; i < fbxIndices.Length; ++i) {
                        var j = fbxIndices[i];
                        if (j < 0) {
                            j = -j - 1;
                        }

                        var b = GetBuilder(fbxMaterial.GetFloat(i));
                        var n = fbxNormals.GetVec3(i);
                        var p = new Vec3(fbxVertices[j * 3], fbxVertices[j * 3 + 2], fbxVertices[j * 3 + 1]);
                        var m = aabb.Normalize(p);
                        b.AddVertex(m, new Vec3(n.X, n.Z, n.Y), fbxUV.GetVec2(i));

                        aabbMesh.Extend(p);
                        if (m.Y < 0.002 && m.X.Abs() < 0.2 && m.Z.Abs() < 0.2) {
                            aabbTrunk.Extend(m);
                        }
                        ++indicesCount;
                    }

                    foreach (var p in dictionary) {
                        var key = (int)p.Key;
                        if (key < 0 || key >= filteredMaterials.Count) throw new Exception("Wrong material index: " + key);
                        var mat = filteredMaterials[key];
                        writer.Write(texturesMap[mat.Textures.FirstOrDefault(t => t.Role == "DiffuseColor")?.Texture.Checksum ?? 0U]);
                        writer.Write(texturesMap[mat.Textures.FirstOrDefault(t => t.Role == "NormalMap")?.Texture.Checksum ?? 0U]);
                        p.Value.Write(writer);
                    }
                }

                var trunkWidth = aabbTrunk.TrunkWidth();
                if (trunkWidth.IsFinite()) {
                    writer.Write(-1);
                    writer.Write(4);
                    writer.Write(trunkWidth);
                }

                return Tuple.Create(aabbMesh, indicesCount / 3, textures.Select(x => x.Texture).ToList());
            }
        }
    }
}