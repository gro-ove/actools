using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Kn5File;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5Utils {
        public static byte[] ToArray(this IKn5 kn5, bool includeTextures = true) {
            using (var memory = new MemoryStream()) {
                if (!includeTextures) {
                    kn5.Textures.Clear();
                    kn5.TexturesData.Clear();
                }
                kn5.Save(memory);
                return memory.ToArray();
            }
        }

        public static List<string> FindBodyMaterials(this IKn5 kn5) {
            var list = kn5.Materials.Values.Where(x => x.ShaderName == "ksPerPixelMultiMap_damage_dirt").Select(x => x.Name).ToList();
            return list.Count > 0 ? list : null;
        }

        public static bool HasCockpitHr(this IKn5 kn5) {
            return kn5.FirstByName("COCKPIT_HR") != null;
        }
    }
}