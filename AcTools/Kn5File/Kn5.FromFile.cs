using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using SlimDX;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public static Kn5 FromFile(string filename, bool skipTextures = false, bool readNodesAsBytes = true) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var kn5 = new Kn5(filename);

            using (var reader = new Kn5Reader(filename)) {
                kn5.FromFile_Header(reader);

                if (skipTextures) {
                    kn5.FromFile_SkipTextures(reader);
                } else {
                    kn5.FromFile_Textures(reader);
                }

                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader, readNodesAsBytes);
            }

            return kn5;
        }

        public void Combine(Kn5 other) {
            RootNode.Children.Add(other.RootNode);
        }

        private float[] CalculateMatrix(float[] position, float[] rotation) {
            var m = Matrix.Translation(position[0], position[1], position[2]) * Matrix.RotationYawPitchRoll(rotation[0], rotation[1], rotation[2]);
            return m.ToArray();
        }

        public void Combine(Kn5 other, float[] position, float[] rotation) {
            if (position.Any(x => !Equals(x, 0f)) || rotation.Any(x => !Equals(x, 0f))) {
                other.RootNode.Transform = CalculateMatrix(position, rotation);
            }

            Combine(other);
        }

        public static Kn5 FromModelsIniFile(string filename, bool skipTextures = false) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var result = CreateEmpty();
            var directory = Path.GetDirectoryName(filename) ?? "";
            foreach (var section in new IniFile(filename).GetSections("MODEL").Select(x => new {
                Filename = Path.Combine(directory, x.GetNonEmpty("FILE") ?? ""),
                Position = x.GetVector3F("POSITION"),
                Rotation = x.GetVector3F("ROTATION")
            }).Where(x => File.Exists(x.Filename))) {
                var kn5 = FromFile(section.Filename, skipTextures);
                result.Combine(kn5, section.Position, section.Rotation);
            }

            return result;
        }

        public static Kn5 FromStream(Stream entry, bool skipTextures = false, bool readNodesAsBytes = true) {
            var kn5 = new Kn5(string.Empty);

            using (var reader = new Kn5Reader(entry)) {
                kn5.FromFile_Header(reader);

                if (skipTextures) {
                    kn5.FromFile_SkipTextures(reader);
                } else {
                    kn5.FromFile_Textures(reader);
                }

                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader, readNodesAsBytes);
            }

            return kn5;
        }

        public static Kn5 FromBytes(byte[] data, bool skipTextures = false, bool readNodesAsBytes = true) {
            using (var memory = new MemoryStream(data)) {
                return FromStream(memory, skipTextures, readNodesAsBytes);
            }
        }

        private void FromFile_Header(Kn5Reader reader) {
            Header = reader.ReadHeader();
        }

        private void FromFile_Textures(Kn5Reader reader) {
            try {
                var count = reader.ReadInt32();

                Textures = new Dictionary<string, Kn5Texture>(count);
                TexturesData = new Dictionary<string, byte[]>(count);

                for (var i = 0; i < count; i++) {
                    var texture = reader.ReadTexture();
                    if (texture.Length > 0) {
                        Textures[texture.Name] = texture;
                        TexturesData[texture.Name] = reader.ReadBytes(texture.Length);
                    }
                }
            } catch (NotImplementedException) {
                Textures = null;
                TexturesData = null;
            }
        }

        private void FromFile_SkipTextures(Kn5Reader reader) {
            try {
                var count = reader.ReadInt32();

                Textures = new Dictionary<string, Kn5Texture>(count);
                TexturesData = new Dictionary<string, byte[]>(count);

                for (var i = 0; i < count; i++) {
                    var texture = reader.ReadTexture();

                    Textures[texture.Name] = texture;
                    TexturesData[texture.Name] = new byte[]{};
                    reader.Skip(texture.Length);
                }
            } catch (NotImplementedException) {
                Textures = null;
                TexturesData = null;
            }
        }

        private void FromFile_Materials(Kn5Reader reader) {
            try {
                var count = reader.ReadInt32();

                Materials = new Dictionary<string, Kn5Material>(count);

                for (var i = 0; i < count; i++) {
                    var material = reader.ReadMaterial();
                    Materials[material.Name] = material;
                }
            } catch (NotImplementedException) {
                Materials = null;
            }
        }

        private void FromFile_Nodes(Kn5Reader reader, bool readAsBytes) {
            if (readAsBytes) {
                var nodesStart = reader.BaseStream.Position;
                var nodesLength = reader.BaseStream.Length - nodesStart;
                NodesBytes = reader.ReadBytes((int)nodesLength);
                reader.BaseStream.Seek(nodesStart, SeekOrigin.Begin);
            }

            try {
                RootNode = FromFile_Node(reader);
            } catch (NotImplementedException) {
                RootNode = null;
            }
        }

        private Kn5Node FromFile_Node(Kn5Reader reader) {
            var node = reader.ReadNode();
            var capacity = node.Children.Capacity;

            try {
                for (var i = 0; i < capacity; i++) {
                    node.Children.Add(FromFile_Node(reader));
                }
            } catch (EndOfStreamException) {
                node.Children.Capacity = node.Children.Count;
            }

            return node;
        }

        public void LoadTexturesFrom(string filename) {
            using (var reader = new Kn5Reader(filename)) {
                reader.ReadHeader();
                FromFile_Textures(reader);
            }
        }
    }
}
