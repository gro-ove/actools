using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5GenericUtils {
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

        public static void RemoveUnusedMaterials(this IKn5 kn5) {
            var materialsToKeep = new List<uint>();
            foreach (var node in kn5.Nodes.Where(x => x.NodeClass != Kn5NodeClass.Base)) {
                if (!materialsToKeep.Contains(node.MaterialId)) {
                    materialsToKeep.Add(node.MaterialId);
                }
            }

            var newMaterials = kn5.Materials.Values
                    .Where((x, i) => materialsToKeep.Contains((uint)i))
                    .ToDictionary(x => x.Name, x => x);
            foreach (var node in kn5.Nodes.Where(x => x.NodeClass != Kn5NodeClass.Base)) {
                var id = newMaterials.Values.IndexOf(kn5.Materials.Values.ElementAt((int)node.MaterialId));
                if (id == -1) {
                    throw new Exception("Unexpected conflict");
                }
                node.MaterialId = (uint)id;
            }
            kn5.Materials = newMaterials;
        }
    }
}