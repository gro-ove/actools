using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using K4os.Compression.LZ4;
using SystemHalf;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5Utils {
        public static byte[] ExportUv2Data(this IKn5 kn5, Func<Kn5Node, bool> filter = null) {
            using (var memory = new MemoryStream()) {
                using (var writer = new ExtendedBinaryWriter(memory, true)) {
                    void Write(Kn5Node node) {
                        var uv2 = node.Uv2;
                        if (uv2 == null) return;
                        
                        node.RecalculateTangents();
                        writer.Write(node.Name);
                        writer.Write(node.Indices.Length);
                        for (int i = 0; i < node.Indices.Length; ++i) {
                            writer.Write(node.Indices[i]);
                        }

                        writer.Write(node.Vertices.Length);
                        for (int i = 0; i < node.Vertices.Length; ++i) {
                            writer.Write(node.Vertices[i].Position);
                            writer.Write(node.Vertices[i].Normal);
                            writer.Write(node.Vertices[i].Tex);

                            // Converted tangent:
                            writer.Write((byte)((node.Vertices[i].Tangent.X * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                            writer.Write((byte)((node.Vertices[i].Tangent.Y * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                            writer.Write((byte)((node.Vertices[i].Tangent.Z * (0.5f * 255f) + (0.5f * 255f)).Clamp(0f, 255f)));
                            writer.Write((byte)0); // element ID
                            writer.WriteHalf(uv2[i].X); // UV2.x
                            writer.WriteHalf(uv2[i].Y); // UV2.y
                            writer.Write((byte)255); // AO0
                            writer.Write((byte)255); // AO1
                            writer.Write((byte)255); // wet
                            writer.Write((byte)0); // extra
                        }
                    }

                    kn5.Nodes.Where(x => x.Uv2 != null && filter?.Invoke(x) != false).ForEach(Write);
                }
                return memory.ToArray();
            }
        }

        public static void SaveUv2(this IKn5 kn5, string filename, bool skipIfEmpty, Func<Kn5Node, bool> filter = null) {
            var data = kn5.ExportUv2Data(filter);
            if (data.Length == 0) return;
            
            var compressed2 = new byte[data.Length];
            var newLength = LZ4Codec.Encode(data, 0, data.Length,
                    compressed2, 0, compressed2.Length, LZ4Level.L09_HC);

            var compressed = new byte[newLength + 5];
            compressed[0] = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, compressed, 1, 4);
            Buffer.BlockCopy(compressed2, 0, compressed, 5, newLength);
            File.WriteAllBytes(filename, compressed);
        }

        public static void LoadUv2(this IKn5 kn5, byte[] data, Func<Kn5Node, bool> filter = null) {
            if (data.Length < 1 || data[0] != 0) throw new Exception($"Unsupported UV2 patch version: {(int)data[0]}");

            var vertexSize = Marshal.SizeOf<Kn5Node.Vertex>();
            if (vertexSize != 44) throw new Exception("Unexpected size");

            var decompressedSize = BitConverter.ToInt32(data, 1);
            var uv2Data = new byte[decompressedSize];

            var newLength = LZ4Codec.Decode(data, 5, data.Length - 5, uv2Data, 0, decompressedSize);
            if (newLength != decompressedSize) throw new Exception("Damaged compression");

            for (var i = 0; i < uv2Data.Length;) {
                var nameLength = BitConverter.ToInt32(uv2Data, i);
                var name = Encoding.UTF8.GetString(uv2Data, i + 4, nameLength);
                i += 4 + nameLength;
                var indicesLength = BitConverter.ToInt32(uv2Data, i);
                var indices = new ushort[indicesLength];
                for (var j = 0; j < indicesLength; ++j) {
                    indices[j] = BitConverter.ToUInt16(uv2Data, i + 4 + j * 2);
                }
                i += 4 + indicesLength * 2;
                var verticesLength = BitConverter.ToInt32(uv2Data, i);
                var vertices = new Kn5Node.Vertex[verticesLength];
                var uv2 = new Vec2[verticesLength];

                i += 4;
                for (var j = 0; j < verticesLength; ++j) {
                    vertices[j].Position.X = BitConverter.ToSingle(uv2Data, i);
                    vertices[j].Position.Y = BitConverter.ToSingle(uv2Data, i + 4);
                    vertices[j].Position.Z = BitConverter.ToSingle(uv2Data, i + 8);
                    vertices[j].Normal.X = BitConverter.ToSingle(uv2Data, i + 12);
                    vertices[j].Normal.Y = BitConverter.ToSingle(uv2Data, i + 16);
                    vertices[j].Normal.Z = BitConverter.ToSingle(uv2Data, i + 20);
                    vertices[j].Tex.X = BitConverter.ToSingle(uv2Data, i + 24);
                    vertices[j].Tex.Y = BitConverter.ToSingle(uv2Data, i + 28);
                    vertices[j].Tangent.X = uv2Data[i + 32] / 127f - 0.5f;
                    vertices[j].Tangent.Y = uv2Data[i + 33] / 127f - 0.5f;
                    vertices[j].Tangent.Z = uv2Data[i + 34] / 127f - 0.5f;
                    uv2[j].X = Half.ToHalf(uv2Data, i + 36);
                    uv2[j].Y = Half.ToHalf(uv2Data, i + 38);
                    i += vertexSize;
                }

                var mesh = kn5.FirstFiltered(x => x.Name == name && x.NodeClass == Kn5NodeClass.Mesh);
                if (mesh == null) throw new Exception($"UV2 patch references missing mesh: {name}");
                if (filter?.Invoke(mesh) == false) continue;

                mesh.Indices = indices;
                mesh.Vertices = vertices;
                mesh.Uv2 = uv2;
            }
        }

        public static byte[] ToArray(this IKn5 kn5, bool includeTextures = true) {
            using (var memory = new MemoryStream()) {
                if (!includeTextures) {
                    kn5.Textures.Clear();
                    kn5.TexturesData.Clear();
                }
                kn5.Save(memory);
                return memory.ToArray();
            }
        }

        public static List<string> FindBodyMaterials(this IKn5 kn5) {
            var list = kn5.Materials.Values.Where(x => x.ShaderName == "ksPerPixelMultiMap_damage_dirt").Select(x => x.Name).ToList();
            return list.Count > 0 ? list : null;
        }

        public static bool HasCockpitHr(this IKn5 kn5) {
            return kn5.FirstByName("COCKPIT_HR") != null;
        }

        public static void RemoveUnusedMaterials(this IKn5 kn5) {
            var materialsToKeep = new List<uint>();
            foreach (var node in kn5.Nodes.Where(x => x.NodeClass != Kn5NodeClass.Base)) {
                if (!materialsToKeep.Contains(node.MaterialId)) {
                    materialsToKeep.Add(node.MaterialId);
                }
            }

            var newMaterials = kn5.Materials.Values
                    .Where((x, i) => materialsToKeep.Contains((uint)i))
                    .ToDictionary(x => x.Name, x => x);
            foreach (var node in kn5.Nodes.Where(x => x.NodeClass != Kn5NodeClass.Base)) {
                var id = newMaterials.Values.IndexOf(kn5.Materials.Values.ElementAt((int)node.MaterialId));
                if (id == -1) {
                    throw new Exception("Unexpected conflict");
                }
                node.MaterialId = (uint)id;
            }
            kn5.Materials = newMaterials;
        }
    }
}