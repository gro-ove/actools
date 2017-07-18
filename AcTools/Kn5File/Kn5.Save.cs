using System;
using System.IO;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5TextureProvider {
        void GetTexture([NotNull] string textureName, Func<int, Stream> writer);
    }

    public partial class Kn5 {
        private static void Save_Node(Kn5Writer writer, Kn5Node node) {
            writer.Write(node);
            foreach (var t in node.Children) {
                Save_Node(writer, t);
            }
        }

        public void Save(string filename, IKn5TextureProvider textureProvider = null) {
            using (var writer = new Kn5Writer(File.Open(filename, FileMode.Create, FileAccess.ReadWrite))) {
                writer.Write(Header);

                writer.Write(Textures.Count);
                foreach (var texture in Textures.Values) {
                    if (TexturesData.TryGetValue(texture.Name, out var data) && data.Length > 0) {
                        texture.Length = data.Length;
                        writer.Write(texture);
                        writer.Write(data);
                    } else {
                        textureProvider?.GetTexture(texture.Name, size => {
                            texture.Length = size;
                            writer.Write(texture);
                            writer.Flush();
                            return writer.BaseStream;
                        });
                    }
                }

                writer.Write(Materials.Count);
                foreach (var material in Materials.Values) {
                    writer.Write(material);
                }

                Save_Node(writer, RootNode);
            }
        }

        public void SaveRecyclingOriginal(string filename, IKn5TextureProvider textureProvider = null) {
            using (var f = FileUtils.RecycleOriginal(filename)) {
                try {
                    Save(f.Filename, textureProvider);
                } catch {
                    FileUtils.TryToDelete(f.Filename);
                    throw;
                }
            }
        }
    }
}
