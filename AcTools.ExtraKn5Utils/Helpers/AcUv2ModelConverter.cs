using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.ExtraKn5Utils.FbxUtils;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.ExtraKn5Utils.Helpers {
    public static class AcUv2ModelConverter {
        private static Dictionary<string, List<MeshBuilder>> CollectBuilders(string fbxFilename) {
            var ret = new Dictionary<string, List<MeshBuilder>>();
            var fbxDocument = FbxIO.Read(fbxFilename);
            foreach (var geometryId in fbxDocument.GetGeometryIds()) {
                var name = fbxDocument.GetNode("Model", fbxDocument.GetConnection(geometryId)).GetName("Model");
                var fbxNode = fbxDocument.GetGeometry(geometryId);
                var dataIndices = fbxNode?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();
                if (dataIndices == null) {
                    AcToolsLogging.Write("Indices data is missing: " + name);
                    continue;
                }
                
                var dataVertices = fbxNode.GetRelative("Vertices").Value.GetAsFloatArray();
                var fbxNormals = fbxNode.GetRelative("LayerElementNormal");
                var fbxMaterials = fbxNode.GetRelative("LayerElementMaterial");
                if (fbxNormals == null || fbxMaterials == null) {
                    AcToolsLogging.Write("Crucial mesh data is missing: " + name);
                    continue;
                }
                
                var fbxUVs = Enumerable.Range(0, 99).Select(x => fbxNode.GetRelative($"LayerElementUV:{x}"))
                        .TakeWhile(x => x != null).Select(x => new FbxDataAccessor(x, "UV")).ToList();
                if (fbxUVs.Count >= 2 && fbxUVs.Skip(1).Any(x => x.GetDataHashCode() != fbxUVs[0].GetDataHashCode())) {
                    if (fbxUVs.Skip(2).All(x => x.GetDataHashCode() == fbxUVs[1].GetDataHashCode())) {
                        fbxUVs = fbxUVs.Take(2).ToList();
                    }

                    var dataNormals = new FbxDataAccessor(fbxNormals, "Normals");
                    var dataMaterials = new FbxDataAccessor(fbxMaterials, "Materials");
                    foreach (var extrasAccessor3 in fbxUVs.Skip(1)) {
                        var materialTable = new Dictionary<float, MeshBuilder>();
                        for (var i = 0; i < dataIndices.Length; ++i) {
                            var j = dataIndices[i];
                            if (j < 0) {
                                if (i % 3 != 2) {
                                    throw new Exception("Mesh is not triangulated");
                                }
                                j = -j - 1;
                            }
                                    
                            GetBuilder(dataMaterials.GetFloat(i)).AddVertex(
                                    new Vec3(dataVertices[j * 3], dataVertices[j * 3 + 1], dataVertices[j * 3 + 2]),
                                    dataNormals.GetVec3(i), fbxUVs[0].GetVec2(i), extrasAccessor3.GetVec2(i));
                        }

                        if (materialTable.Count == 1) {
                            AddItem(materialTable.First().Value);
                        } else {
                            foreach (var meshBuilder in materialTable.Values) {
                                AddItem(meshBuilder);
                            }
                        }

                        MeshBuilder GetBuilder(float material) {
                            return materialTable.GetValueOrSet(material, () => {
                                if (materialTable.Count == 1) {
                                    materialTable.First().Value.SetSub();
                                }
                                return new MeshBuilder(name, materialTable.Count);
                            });
                        }
                    }
                }
            }
            return ret;

            void AddItem(MeshBuilder item) {
                ret.GetValueOrSet(item.Name, () => new List<MeshBuilder>()).Add(item);
            }
        }

        public static int Convert(string fbxFilename, string filename) {
            var builders = CollectBuilders(fbxFilename);
            if (builders.Count == 0) {
                throw new Exception("No meshes with more than one UV set are found");
            }
            
            var uniqueSets = builders.Values.Select(x => x.Count).Max();
            for (var i = 0; i < uniqueSets; ++i) {
                using (var memory = new MemoryStream()) {
                    using (var writer = new ExtendedBinaryWriter(memory, true)) {
                        foreach (var builder in builders.Values) {
                            builder[i >= builder.Count ? 0 : i].Write(writer);
                        }
                    }
                    Kn5ExtendedUtils.SaveUv2Data(i == 0 ? filename : filename.Replace(".uv2", $".{i + 2}.uv2"), memory.ToArray());
                }
            }
            return uniqueSets;
        }

        private class MeshBuilder {
            private readonly List<ushort> _indices = new List<ushort>();
            private readonly List<Kn5Node.Vertex> _vertices = new List<Kn5Node.Vertex>();
            private readonly Dictionary<Kn5Node.Vertex, int> _knownVertices = new Dictionary<Kn5Node.Vertex, int>();

            public string Name { get; private set; }

            public MeshBuilder(string name, int index) {
                Name = index == 0 ? name : name + "_SUB" + index;
            }

            public void SetSub() {
                Name += "_SUB0";
            }

            public void AddVertex(Vec3 pos, Vec3 normal, Vec2 uv1, Vec2 uv2) {
                var key = new Kn5Node.Vertex(pos, normal, uv1, new Vec3(uv2, 0f));
                if (_knownVertices.TryGetValue(key, out var index)) {
                    _indices.Add((ushort)index);
                } else {
                    var count = _vertices.Count;
                    _knownVertices[key] = count;
                    _vertices.Add(key);
                    _indices.Add((ushort)count);
                }
            }

            public void Write(ExtendedBinaryWriter writer) {
                var kn5Node = new Kn5Node {
                    NodeClass = (Kn5NodeClass)2,
                    Indices = _indices.ToArray(),
                    Vertices = _vertices.ToArray()
                };
                kn5Node.RecalculateTangents();
                writer.Write(Name);
                writer.Write(kn5Node.Indices.Length);
                for (var i = 0; i < kn5Node.Indices.Length; ++i) {
                    writer.Write(kn5Node.Indices[i]);
                }
                writer.Write(kn5Node.Vertices.Length);
                for (var i = 0; i < kn5Node.Vertices.Length; ++i) {
                    writer.Write(kn5Node.Vertices[i].Position);
                    writer.Write(kn5Node.Vertices[i].Normal);
                    writer.Write(kn5Node.Vertices[i].Tex);
                    writer.Write((byte)((float)(kn5Node.Vertices[i].Tangent.X * 127.5 + 127.5)).Clamp(0f, byte.MaxValue));
                    writer.Write((byte)((float)(kn5Node.Vertices[i].Tangent.Y * 127.5 + 127.5)).Clamp(0f, byte.MaxValue));
                    writer.Write((byte)((float)(kn5Node.Vertices[i].Tangent.Z * 127.5 + 127.5)).Clamp(0f, byte.MaxValue));
                    writer.Write((byte)0);
                    writer.WriteHalf(_vertices[i].Tangent.X);
                    writer.WriteHalf(_vertices[i].Tangent.Y);
                    writer.Write(byte.MaxValue);
                    writer.Write(byte.MaxValue);
                    writer.Write(byte.MaxValue);
                    writer.Write((byte)0);
                }
            }
        }
    }
}