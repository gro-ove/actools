using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public string OriginalFilename { get; }

        private Kn5() {
            OriginalFilename = string.Empty;
            Header = new Kn5Header { Version = 5 };
            Textures = new Dictionary<string, Kn5Texture>();
            TexturesData = new Dictionary<string, byte[]>();
            Materials = new Dictionary<string, Kn5Material>();
            NodesBytes = new byte[0];
        }

        private Kn5(string filename) {
            OriginalFilename = filename;
        }

        public Kn5Header Header;

        public Dictionary<string, Kn5Texture> Textures;
        public Dictionary<string, byte[]> TexturesData;

        public Dictionary<string, Kn5Material> Materials;

        public Kn5Node RootNode;
        public byte[] NodesBytes;

        public enum ExportType {
            Collada,
            Fbx,
            FbxIni,
            FbxWithIni,
            Directory
        }

        public IEnumerable<Kn5Node> Nodes {
            get {
                var queue = new Queue<Kn5Node>();
                if (RootNode != null) {
                    queue.Enqueue(RootNode);
                }

                while (queue.Count > 0) {
                    var next = queue.Dequeue();

                    if (next.NodeClass == Kn5NodeClass.Base) {
                        foreach (var child in next.Children) {
                            queue.Enqueue(child);
                        }
                    }

                    yield return next;
                }
            }
        }

        public Kn5Node FirstByName(string name) {
            return Nodes.FirstOrDefault(node => node.Name == name);
        }

        public int RemoveAllByName(Kn5Node node, string name) {
            var result = 0;
            for (var i = 0; i < node.Children.Count; i++) {
                var child = node.Children[i];
                if (child.Name == name) {
                    node.Children.Remove(child);
                    result++;
                } else if (child.NodeClass == Kn5NodeClass.Base) {
                    result += RemoveAllByName(child, name);
                }
            }

            return result;
        }

        public int RemoveAllByName(string name) {
            return RemoveAllByName(RootNode, name);
        }

        public Kn5Node FirstFiltered(Func<Kn5Node, bool> filter) {
            return Nodes.FirstOrDefault(filter);
        }

        public void Export(ExportType type, string filename) {
            switch (type) {
                case ExportType.Collada:
                    ExportCollada(filename);
                    break;

                case ExportType.Fbx:
                    ExportFbx(filename);
                    break;

                case ExportType.FbxIni:
                    ExportIni(filename);
                    break;

                case ExportType.FbxWithIni:
                    ExportFbx(filename);
                    ExportIni(filename + ".ini", Path.GetFileName(filename));
                    break;

                case ExportType.Directory:
                    ExportDirectory(filename);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public void PrintDebugInformation() {
            Console.WriteLine(@"Version: {0}", Header.Version);
            if (Header.Extra != 0) {
                Console.WriteLine(@"Extra: {0}", Header.Extra);
                Console.ReadKey();
            }

            if (Textures != null) {
                Console.WriteLine(@"Textures: {0}", Textures.Count);
            } else {
                Console.Error.WriteLine("Cannot read textures");
            }

            if (Materials != null) {
                Console.WriteLine(@"Materials: {0}", Materials.Count);
            } else {
                Console.Error.WriteLine("Cannot read materials");
            }

            if (RootNode != null) {
                Console.WriteLine(@"Root node: {0}", RootNode.Name);
            } else {
                Console.Error.WriteLine("Cannot read nodes");
            }
        }
    }
}
