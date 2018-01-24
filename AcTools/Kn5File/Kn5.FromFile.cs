using System;
using System.Collections.Generic;
using System.IO;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5TextureLoader {
        void OnNewKn5(string kn5Filename);

        [CanBeNull]
        byte[] LoadTexture([NotNull] string textureName, [NotNull] ReadAheadBinaryReader reader, int textureSize);
    }

    public class SkippingTextureLoader : IKn5TextureLoader {
        [NotNull]
        public static readonly SkippingTextureLoader Instance = new SkippingTextureLoader();

        private SkippingTextureLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
            reader.Skip(textureSize);
            return null;
        }
    }

    public class DefaultKn5TextureLoader : IKn5TextureLoader {
        [NotNull]
        public static readonly DefaultKn5TextureLoader Instance = new DefaultKn5TextureLoader();

        private DefaultKn5TextureLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
            var result = MemoryChunk.Bytes(textureSize).Execute(() => new byte[textureSize]);
            reader.ReadBytesTo(result, 0, textureSize);
            return result;
        }
    }

    public interface IKn5MaterialLoader {
        void OnNewKn5(string kn5Filename);

        [CanBeNull]
        Kn5Material LoadMaterial([NotNull] ReadAheadBinaryReader reader);
    }

    public class SkippingMaterialLoader : IKn5MaterialLoader {
        [NotNull]
        public static readonly SkippingMaterialLoader Instance = new SkippingMaterialLoader();

        private SkippingMaterialLoader() {}
        public void OnNewKn5(string kn5Filename) { }

        public Kn5Material LoadMaterial(ReadAheadBinaryReader reader) {
            ((Kn5Reader)reader).SkipMaterial();
            return null;
        }
    }

    public class DefaultKn5MaterialLoader : IKn5MaterialLoader {
        [NotNull]
        public static readonly DefaultKn5MaterialLoader Instance = new DefaultKn5MaterialLoader();

        private DefaultKn5MaterialLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        public Kn5Material LoadMaterial(ReadAheadBinaryReader reader) {
            return ((Kn5Reader)reader).ReadMaterial();
        }
    }

    public interface IKn5NodeLoader {
        void OnNewKn5(string kn5Filename);

        [CanBeNull]
        Kn5Node LoadNode([NotNull] ReadAheadBinaryReader reader);
    }

    public class HierarchyOnlyNodeLoader : IKn5NodeLoader {
        [NotNull]
        public static readonly HierarchyOnlyNodeLoader Instance = new HierarchyOnlyNodeLoader();

        private HierarchyOnlyNodeLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        private static Kn5Node LoadHierarchy(Kn5Reader reader) {
            var node = reader.ReadNodeHierarchy();
            var capacity = node.Children.Capacity;

            try {
                for (var i = 0; i < capacity; i++) {
                    node.Children.Add(LoadHierarchy(reader));
                }
            } catch (EndOfStreamException) {
                node.Children.Capacity = node.Children.Count;
            }

            return node;
        }

        public Kn5Node LoadNode(ReadAheadBinaryReader reader) {
            return LoadHierarchy((Kn5Reader)reader);
        }
    }

    public class SkippingNodeLoader : IKn5NodeLoader {
        [NotNull]
        public static readonly SkippingNodeLoader Instance = new SkippingNodeLoader();

        private SkippingNodeLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        private static Kn5Node SkipNode(Kn5Reader reader) {
            var node = reader.ReadNodeHierarchy();
            var capacity = node.Children.Capacity;

            try {
                for (var i = 0; i < capacity; i++) {
                    node.Children.Add(SkipNode(reader));
                }
            } catch (EndOfStreamException) {
                node.Children.Capacity = node.Children.Count;
            }

            return node;
        }

        public Kn5Node LoadNode(ReadAheadBinaryReader reader) {
            SkipNode((Kn5Reader)reader);
            return null;
        }
    }

    public class DefaultKn5NodeLoader : IKn5NodeLoader {
        [NotNull]
        public static readonly DefaultKn5NodeLoader Instance = new DefaultKn5NodeLoader();

        private DefaultKn5NodeLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        private static Kn5Node LoadNode(Kn5Reader reader) {
            var node = reader.ReadNode();
            var capacity = node.Children.Capacity;

            try {
                for (var i = 0; i < capacity; i++) {
                    node.Children.Add(LoadNode(reader));
                }
            } catch (EndOfStreamException) {
                node.Children.Capacity = node.Children.Count;
            }

            return node;
        }

        public Kn5Node LoadNode(ReadAheadBinaryReader reader) {
            return LoadNode((Kn5Reader)reader);
        }
    }

    public partial class Kn5 {
        [NotNull]
        public static Kn5 FromFile(string filename, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                IKn5NodeLoader nodeLoader = null) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var kn5 = new Kn5(filename);
            (textureLoader = textureLoader ?? DefaultKn5TextureLoader.Instance).OnNewKn5(filename);
            (materialLoader = materialLoader ?? DefaultKn5MaterialLoader.Instance).OnNewKn5(filename);
            (nodeLoader = nodeLoader ?? DefaultKn5NodeLoader.Instance).OnNewKn5(filename);

            using (var reader = new Kn5Reader(filename)) {
                kn5.FromFile_Header(reader);
                kn5.FromFile_Textures(reader, textureLoader);
                if (nodeLoader != SkippingNodeLoader.Instance || materialLoader != SkippingMaterialLoader.Instance) {
                    kn5.FromFile_Materials(reader, materialLoader);
                    kn5.FromFile_Nodes(reader, nodeLoader);
                }
            }

            return kn5;
        }

        [NotNull]
        public static Kn5 FromStream(Stream entry, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                IKn5NodeLoader nodeLoader = null) {
            var kn5 = new Kn5(string.Empty);
            (textureLoader = textureLoader ?? DefaultKn5TextureLoader.Instance).OnNewKn5(string.Empty);
            (materialLoader = materialLoader ?? DefaultKn5MaterialLoader.Instance).OnNewKn5(string.Empty);
            (nodeLoader = nodeLoader ?? DefaultKn5NodeLoader.Instance).OnNewKn5(string.Empty);

            using (var reader = new Kn5Reader(entry)) {
                kn5.FromFile_Header(reader);
                kn5.FromFile_Textures(reader, textureLoader);
                if (nodeLoader != SkippingNodeLoader.Instance || materialLoader != SkippingMaterialLoader.Instance) {
                    kn5.FromFile_Materials(reader, materialLoader);
                    kn5.FromFile_Nodes(reader, nodeLoader);
                }
            }

            return kn5;
        }

        [NotNull]
        public static Kn5 FromBytes(byte[] data, IKn5TextureLoader textureLoader = null) {
            using (var memory = new MemoryStream(data)) {
                return FromStream(memory, textureLoader);
            }
        }

        private void FromFile_Header(Kn5Reader reader) {
            Header = reader.ReadHeader();
        }

        public IKn5TextureLoader TextureLoader { get; private set; }

        private void FromFile_Textures(Kn5Reader reader, [NotNull] IKn5TextureLoader textureLoader) {
            TextureLoader = textureLoader;

            try {
                var count = reader.ReadInt32();

                Textures = new Dictionary<string, Kn5Texture>(count);
                TexturesData = new Dictionary<string, byte[]>(count);

                for (var i = 0; i < count; i++) {
                    var texture = reader.ReadTexture();
                    if (texture.Length > 0) {
                        Textures[texture.Name] = texture;
                        TexturesData[texture.Name] = textureLoader.LoadTexture(texture.Name, reader, texture.Length) ?? new byte[0];
                    }
                }
            } catch (NotImplementedException) {
                Textures = null;
                TexturesData = null;
            }
        }

        public IKn5MaterialLoader MaterialLoader { get; private set; }

        private void FromFile_Materials(Kn5Reader reader, [NotNull] IKn5MaterialLoader materialLoader) {
            MaterialLoader = materialLoader;

            try {
                var count = reader.ReadInt32();

                Materials = new Dictionary<string, Kn5Material>(count);
                for (var i = 0; i < count; i++) {
                    var material = materialLoader.LoadMaterial(reader);
                    if (material != null) {
                        Materials[material.Name] = material;
                    }
                }
            } catch (NotImplementedException) {
                Materials = null;
            }
        }

        public IKn5NodeLoader NodeLoader { get; private set; }

        private void FromFile_Nodes(Kn5Reader reader, [NotNull] IKn5NodeLoader nodeLoader) {
            NodeLoader = nodeLoader;
            RootNode = nodeLoader.LoadNode(reader);
        }
    }
}
