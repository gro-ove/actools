using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented
        };

        private static Regex _jsonRegex;

        public void ExportHeaderJson(string file) {
            File.WriteAllText(file, JsonConvert.SerializeObject(Header, JsonSettings));
        }

        public void ExportTexturesJson(string file) {
            var array = new Kn5Texture[Textures.Count];
            for (var i = 0; i < array.Length; i++) {
                array[i] = Textures.Values.ElementAt(i);
            }
            
            File.WriteAllText(file, JsonConvert.SerializeObject(array, JsonSettings));
        }

        public void ExportMaterialsJson(string file) {
            var array = new Kn5Material[Materials.Count];
            for (var i = 0; i < array.Length; i++) {
                array[i] = Materials.Values.ElementAt(i);
            }

            if (_jsonRegex == null){
                _jsonRegex = new Regex(@"\r?\n      (\s+|(\}))", RegexOptions.Compiled);
            }
            
            File.WriteAllText(file, _jsonRegex.Replace(JsonConvert.SerializeObject(array, JsonSettings), m => m.Groups[2].Success ? " " + m.Groups[2].Value : " "));
        }

        public void ExportNodesJson(string file) {
            File.WriteAllText(file, JsonConvert.SerializeObject(RootNode, JsonSettings));
        }
    }
}
