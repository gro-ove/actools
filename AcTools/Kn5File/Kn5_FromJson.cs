using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public Kn5Header FromHeaderJson(string file) {
            return JsonConvert.DeserializeObject<Kn5Header>(File.ReadAllText(file));
        }

        public Dictionary<string,Kn5Material> FromMaterialsJson(string file) {
            var array = JsonConvert.DeserializeObject<Kn5Material[]>(File.ReadAllText(file));
            var materials = new Dictionary<string,Kn5Material>(array.Length);
            foreach (var t in array) {
                materials[t.Name] = t;
            }

            return materials;
        }

        public Kn5Node FromNodesJson(string file) {
            return JsonConvert.DeserializeObject<Kn5Node>(File.ReadAllText(file));
        }
    }
}
