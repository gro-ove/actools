using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Kn5Specific.Materials {
    public static class Kn5MaterialsProvider {
        private static Kn5 _kn5;
        private static IKn5MaterialsProvider _instance;

        public static void Initialize(IKn5MaterialsProvider materialProvider) {
            _instance = materialProvider;
        }

        public static void SetKn5(Kn5 kn5) {
            _kn5 = kn5;
        }

        public static void DisposeAll() {
            Materials.DisposeEverything();
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

        private static IRenderableMaterial GetOrCreate(string key, Func<IRenderableMaterial> create) {
            IRenderableMaterial material;
            if (Materials.TryGetValue(key, out material)) return material;
            return Materials[key] = create();
        }

        public static IRenderableMaterial GetMaterial(uint materialId) => GetOrCreate(_kn5.OriginalFilename + "//" + materialId,
                () => _instance.CreateMaterial(_kn5.OriginalFilename, _kn5.Materials.Values.ElementAtOrDefault((int)materialId)));

        public static IRenderableMaterial GetAmbientShadowMaterial(string filename) => GetOrCreate("//ambientshadow//" + filename,
                () => _instance.CreateAmbientShadowMaterial(filename));

        public static IRenderableMaterial GetSkyMaterial() => GetOrCreate("//sky",
                () => _instance.CreateSkyMaterial());

        public static IRenderableMaterial GetMirrorMaterial() => GetOrCreate("//mirror",
                () => _instance.CreateMirrorMaterial());
    }
}
