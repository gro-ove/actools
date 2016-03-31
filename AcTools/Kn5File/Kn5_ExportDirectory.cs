using System.IO;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public void ExportDirectory(string dir, bool nodesJson = false) {
            Directory.CreateDirectory(dir);
            ExportDirectory_Header(dir);
            ExportDirectory_Textures(dir);
            ExportDirectory_Materials(dir);
            ExportDirectory_NodesBinary(dir);
            if (nodesJson) {
                ExportDirectory_NodesJson(dir);
            }
        }

        private void ExportDirectory_Header(string dir) {
            ExportHeaderJson(Path.Combine(dir, "header.json"));
        }

        private void ExportDirectory_Textures(string dir) {
            Directory.CreateDirectory(Path.Combine(dir, "texture"));

            foreach (var texture in Textures.Values) {
                File.WriteAllBytes(Path.Combine(dir, "texture", texture.Filename), TexturesData[texture.Filename]);
            }

            ExportTexturesJson(Path.Combine(dir, "textures.json"));
        }

        private void ExportDirectory_Materials(string dir) {
            ExportMaterialsJson(Path.Combine(dir, "materials.json"));
        }

        private void ExportDirectory_NodesBinary(string dir) {
            File.WriteAllBytes(Path.Combine(dir, "model.bin"), NodesBytes);
        }

        private void ExportDirectory_NodesJson(string dir) {
            ExportNodesJson(Path.Combine(dir, "model.json"));
        }
    }
}
