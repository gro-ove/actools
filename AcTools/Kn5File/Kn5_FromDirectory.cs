using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public static Kn5 FromDirectory(string dir, bool jsonMode) {
            if (!Directory.Exists(dir)) {
                throw new DirectoryNotFoundException(dir);
            }

            var kn5 = new Kn5(dir);
            
            kn5.FromDirectory_Header(dir);
            kn5.FromDirectory_Textures(dir);
            kn5.FromDirectory_Materials(dir);
            kn5.FromDirectory_Nodes(dir, jsonMode);

            return kn5;
        }

        private void FromDirectory_Header(string dir) {
            Header = FromHeaderJson(Path.Combine(dir, "header.json"));
        }

        private void FromDirectory_Textures(string dir) {
            var array = JsonConvert.DeserializeObject<Kn5Texture[]>(File.ReadAllText(Path.Combine(dir, "textures.json")));

            Textures = new Dictionary<string,Kn5Texture>(array.Length);
            TexturesData = new Dictionary<string,byte[]>(array.Length);

            foreach (var texture in array) {
                var data = File.ReadAllBytes(Path.Combine(dir, "texture", texture.Filename));
                texture.Length = data.Length;

                Textures[texture.Name] = texture;
                TexturesData[texture.Filename] = data;
            }
        }

        private void FromDirectory_Materials(string dir) {
            Materials = FromMaterialsJson(Path.Combine(dir, "materials.json"));
        }

        private void FromDirectory_Nodes(string dir, bool jsonMode) {
            if (jsonMode && File.Exists(Path.Combine(dir, "model.json"))) {
                RootNode = FromNodesJson(Path.Combine(dir, "model.json"));
            } else {
                RootNode = null;
            }

            NodesBytes = File.ReadAllBytes(Path.Combine(dir, "model.bin"));
        }
    }
}
