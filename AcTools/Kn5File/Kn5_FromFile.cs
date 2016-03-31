using System;
using System.Collections.Generic;
using System.IO;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public static Kn5 FromFile(string filename, bool skipTextures) {
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
                kn5.FromFile_Nodes(reader);
            }

            return kn5;
        }

        public static Kn5 FromStream(Stream entry, bool skipTextures) {
            var kn5 = new Kn5(string.Empty);

            using (var reader = new Kn5Reader(entry)) {
                kn5.FromFile_Header(reader);

                if (skipTextures) {
                    kn5.FromFile_SkipTextures(reader);
                } else {
                    kn5.FromFile_Textures(reader);
                }

                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader);
            }

            return kn5;
        }

        public static Kn5 FromFile(string filename) {
            return FromFile(filename, false);
        }

        public static Kn5 FromStream(Stream filename) {
            return FromStream(filename, false);
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
                    if (!Textures.ContainsKey(texture.Name)) {
                        Textures[texture.Name] = texture;
                        TexturesData[texture.Filename] = reader.ReadBytes(texture.Length);
                    } else {
                        reader.BaseStream.Seek(texture.Length, SeekOrigin.Current);
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
                    TexturesData[texture.Filename] = new byte[]{};
                    reader.BaseStream.Seek(texture.Length, SeekOrigin.Current);
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

        private void FromFile_Nodes(Kn5Reader reader) {
            var nodesStart = reader.BaseStream.Position;
            var nodesLength = reader.BaseStream.Length - nodesStart;
            NodesBytes = reader.ReadBytes((int)nodesLength);
            reader.BaseStream.Seek(nodesStart, SeekOrigin.Begin);

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
