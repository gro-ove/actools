using System;
using System.IO;
using AcTools.Utils;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        private void SaveInner(string filename, bool saveNodes, bool replaceIfExists) {
            using (var writer = new Kn5Writer(File.Open(filename, replaceIfExists ? FileMode.Create : FileMode.CreateNew, FileAccess.ReadWrite))) {
                writer.Write(Header);

                writer.Write(Textures.Count);
                foreach (var texture in Textures.Values) {
                    var data = TexturesData[texture.Name];
                    texture.Length = data.Length;
                    writer.Write(texture);
                    writer.Write(data);
                }

                writer.Write(Materials.Count);
                foreach (var material in Materials.Values) {
                    writer.Write(material);
                }

                if (saveNodes) {
                    Save_Node(writer, RootNode);
                } else {
                    if (NodesBytes == null) {
                        throw new Exception("NodesBytes = null");
                    }

                    writer.Write(NodesBytes);
                }
            }
        }

        /// <summary>
        /// Saves file under a new name and then renames it back to “filename” to avoid
        /// breaking stuff if saving goes wrong. Optionally, will move original file
        /// to the Recycle Bin.
        /// </summary>
        public void Save(string filename, bool backup = true) {
            var newFilename = FileUtils.EnsureUnique(filename);
            if (newFilename != filename) {
                if (backup) {
                    FileUtils.Recycle(newFilename);
                }
            }

            SaveInner(newFilename, true, true);

            if (newFilename != filename) {
                if (File.Exists(filename)) {
                    File.Delete(filename);
                }

                File.Move(newFilename, filename);
            }
        }

        private static void Save_Node(Kn5Writer writer, Kn5Node node) {
            writer.Write(node);
            foreach (var t in node.Children) {
                Save_Node(writer, t);
            }
        }
    }
}
