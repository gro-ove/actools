using System;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public void SaveAll(string filename) {
#pragma warning disable 612
            Save(filename, true);
#pragma warning restore 612
        }

        [Obsolete]
        public void Save(string filename, bool saveNodes = false) {
            using (var writer = new Kn5Writer(filename)) {
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

        private static void Save_Node(Kn5Writer writer, Kn5Node node) {
            writer.Write(node);
            foreach (var t in node.Children) {
                Save_Node(writer, t);
            }
        }
    }
}
