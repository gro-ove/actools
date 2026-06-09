using System;
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

        private SkippingMaterialLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        public Kn5Material LoadMaterial(ReadAheadBinaryReader reader) {
            ((IKn5Reader)reader).SkipMaterial();
            return null;
        }
    }

    public class DefaultKn5MaterialLoader : IKn5MaterialLoader {
        [NotNull]
        public static readonly DefaultKn5MaterialLoader Instance = new DefaultKn5MaterialLoader();

        private DefaultKn5MaterialLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        public Kn5Material LoadMaterial(ReadAheadBinaryReader reader) {
            return ((IKn5Reader)reader).ReadMaterial();
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

        private static Kn5Node LoadHierarchy(IKn5Reader reader) {
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
            return LoadHierarchy((IKn5Reader)reader);
        }
    }

    public class SkippingNodeLoader : IKn5NodeLoader {
        [NotNull]
        public static readonly SkippingNodeLoader Instance = new SkippingNodeLoader();

        private SkippingNodeLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        private static Kn5Node SkipNode(IKn5Reader reader) {
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
            SkipNode((IKn5Reader)reader);
            return null;
        }
    }

    public class DefaultKn5NodeLoader : IKn5NodeLoader {
        [NotNull]
        public static readonly DefaultKn5NodeLoader Instance = new DefaultKn5NodeLoader();

        private DefaultKn5NodeLoader() { }
        public void OnNewKn5(string kn5Filename) { }

        private static Kn5Node LoadNode(IKn5Reader reader) {
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
            return LoadNode((IKn5Reader)reader);
        }
    }
}