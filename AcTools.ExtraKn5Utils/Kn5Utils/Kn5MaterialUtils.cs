using System.Collections.Generic;
using AcTools.Kn5File;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5MaterialUtils {
        public static Kn5Material Create(string name) {
            return new Kn5Material {
                ShaderName = "ksPerPixel",
                Name = name,
                ShaderProperties = new Kn5Material.ShaderProperty[0],
                TextureMappings = new Kn5Material.TextureMapping[0]
            };
        }

        public static uint? IndexOf(this Dictionary<string, Kn5Material> materials, [CanBeNull] string name) {
            if (name != null) {
                var retVal = 0U;
                foreach (var item in materials) {
                    if (item.Key == name) return retVal;
                    retVal++;
                }
            }
            return null;
        }
    }
}