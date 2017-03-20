using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5TextureLoader {
        [CanBeNull]
        byte[] LoadTexture([NotNull] string textureName, [NotNull] Stream stream, int textureSize);
    }

    public class BlankKn5TextureLoader : IKn5TextureLoader {
        public static readonly BlankKn5TextureLoader Instance = new BlankKn5TextureLoader();

        public byte[] LoadTexture(string textureName, Stream stream, int textureSize) {
            stream.Seek(textureSize, SeekOrigin.Current);
            return null;
        }
    }

    public class MemoryChunk {
        private readonly int _sizeInMegabytes;

        private MemoryChunk(int sizeInMegabytes) {
            _sizeInMegabytes = sizeInMegabytes;
        }

        public static MemoryChunk Bytes(int bytes) {
            return new MemoryChunk(10 + bytes / 1024 / 1024);
        }

        public static MemoryChunk Megabytes(int megabytes) {
            return new MemoryChunk(megabytes);
        }

        private T ExecuteInner<T>(Func<T> action) {
            if (_sizeInMegabytes < 10) return action();
            using (new MemoryFailPoint(_sizeInMegabytes)) return action();
        }

        public T Execute<T>(Func<T> action) {
            try {
                return ExecuteInner(action);
            } catch (OutOfMemoryException) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return ExecuteInner(action);
            }
        }

        public void Execute(Action action) {
            Execute(() => {
                action();
                return 0;
            });
        }
    }

    internal class DefaultKn5TextureLoader : IKn5TextureLoader {
        public static readonly DefaultKn5TextureLoader Instance = new DefaultKn5TextureLoader();

        public byte[] LoadTexture(string textureName, Stream stream, int textureSize) {
            var result = MemoryChunk.Bytes(textureSize).Execute(() => new byte[textureSize]);
            stream.Read(result, 0, textureSize);
            return result;
        }
    }

    public partial class Kn5 {
        [Obsolete]
        public static Kn5 FromFile(string filename, bool skipTextures, bool readNodesAsBytes = true) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var kn5 = new Kn5(filename);

            using (var reader = new Kn5Reader(filename)) {
                kn5.FromFile_Header(reader);

                if (skipTextures) {
                    kn5.FromFile_Textures(reader, BlankKn5TextureLoader.Instance);
                } else {
                    kn5.FromFile_Textures(reader, DefaultKn5TextureLoader.Instance);
                }

                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader, readNodesAsBytes);
            }

            return kn5;
        }

        public static Kn5 FromFile(string filename, IKn5TextureLoader textureLoader = null) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var kn5 = new Kn5(filename);

            using (var reader = new Kn5Reader(filename)) {
                kn5.FromFile_Header(reader);
                kn5.FromFile_Textures(reader, textureLoader ?? DefaultKn5TextureLoader.Instance);
                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader, false);
            }

            return kn5;
        }

        public static Kn5 FromStream(Stream entry, IKn5TextureLoader textureLoader = null) {
            var kn5 = new Kn5(string.Empty);

            using (var reader = new Kn5Reader(entry)) {
                kn5.FromFile_Header(reader);
                kn5.FromFile_Textures(reader, textureLoader ?? DefaultKn5TextureLoader.Instance);
                kn5.FromFile_Materials(reader);
                kn5.FromFile_Nodes(reader, false);
            }

            return kn5;
        }

        public static Kn5 FromBytes(byte[] data, IKn5TextureLoader textureLoader = null) {
            using (var memory = new MemoryStream(data)) {
                return FromStream(memory, textureLoader);
            }
        }

        private void FromFile_Header(Kn5Reader reader) {
            Header = reader.ReadHeader();
        }

        private void FromFile_Textures(Kn5Reader reader, [NotNull] IKn5TextureLoader textureLoader) {
            try {
                var count = reader.ReadInt32();

                Textures = new Dictionary<string, Kn5Texture>(count);
                TexturesData = new Dictionary<string, byte[]>(count);

                for (var i = 0; i < count; i++) {
                    var texture = reader.ReadTexture();
                    if (texture.Length > 0) {
                        Textures[texture.Name] = texture;
                        TexturesData[texture.Name] = textureLoader.LoadTexture(texture.Name, reader.BaseStream, texture.Length) ?? new byte[0];
                    }
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
    }
}
