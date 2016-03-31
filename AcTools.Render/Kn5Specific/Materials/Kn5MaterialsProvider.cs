using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;

namespace AcTools.Render.Kn5Specific.Materials {
    public static class Kn5MaterialsProvider {
        private static Kn5 _kn5;

        public static void Initialize(Kn5 kn5) {
            _kn5 = kn5;
        }

        public static void DisposeAll() {
            foreach (var material in Materials.Values) {
                material.Dispose();
            }
            Materials.Clear();
        }

        public static void DisposeFor(Kn5 kn5) {
            var keyPrefix = _kn5.OriginalFilename + "//";
            var keys = Materials.Keys.Where(x => x.StartsWith(keyPrefix)).ToList();
            foreach (var key in keys) {
                Materials[key].Dispose();
                Materials.Remove(key);
            }
        }

        private static readonly Dictionary<string, IRenderableMaterial> Materials = new Dictionary<string, IRenderableMaterial>(); 

        public static IRenderableMaterial GetMaterial(uint materialId) {
            var key = _kn5.OriginalFilename + "//" + materialId;
            if (Materials.ContainsKey(key)) return Materials[key];

            var kn5Material = _kn5.Materials.Values.ElementAtOrDefault((int) materialId);
            return Materials[key] = CreateMaterial(_kn5.OriginalFilename, kn5Material);
        }

        private static IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            if (kn5Material.ShaderName == "GL") {
                return new Kn5RenderableSpecialGlMaterial();
            }
            
            return new Kn5RenderableMaterial(_kn5.OriginalFilename, kn5Material);
        }
    }
}
