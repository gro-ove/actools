using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Materials {
    public abstract class Kn5MaterialsProvider : IDisposable {
        private Kn5 _kn5;

        public void SetKn5(Kn5 kn5) {
            _kn5 = kn5;
        }

        public void Dispose() {
            _materials.DisposeEverything();
        }

        public void DisposeFor(Kn5 kn5) {
            var keyPrefix = _kn5.OriginalFilename + "//";
            var keys = _materials.Keys.Where(x => x.StartsWith(keyPrefix)).ToList();
            foreach (var key in keys) {
                _materials[key].Dispose();
                _materials.Remove(key);
            }
        }

        private readonly Dictionary<string, IRenderableMaterial> _materials = new Dictionary<string, IRenderableMaterial>();

        protected virtual IRenderableMaterial GetOrCreate(string key, Func<IRenderableMaterial> create) {
            IRenderableMaterial material;
            if (_materials.TryGetValue(key, out material)) return material;
            return _materials[key] = create();
        }

        public IRenderableMaterial GetMaterial(uint materialId) => GetOrCreate(_kn5.OriginalFilename + "//" + materialId,
                () => CreateMaterial(_kn5.OriginalFilename, _kn5.Materials.Values.ElementAtOrDefault((int)materialId)));

        public IRenderableMaterial GetAmbientShadowMaterial(string filename) => GetOrCreate("//ambientshadow//" + filename,
                () => CreateAmbientShadowMaterial(filename));

        public IRenderableMaterial GetSkyMaterial() => GetOrCreate("//sky",
                CreateSkyMaterial);

        public IRenderableMaterial GetMirrorMaterial() => GetOrCreate("//mirror",
                CreateMirrorMaterial);

        public IRenderableMaterial GetFlatMirrorMaterial() => GetOrCreate("//flatmirror",
                CreateFlatMirrorMaterial);

        public abstract IRenderableMaterial CreateMaterial(string kn5Filename, [CanBeNull]Kn5Material kn5Material);

        public abstract IRenderableMaterial CreateAmbientShadowMaterial(string filename);

        public abstract IRenderableMaterial CreateSkyMaterial();

        public abstract IRenderableMaterial CreateMirrorMaterial();

        public abstract IRenderableMaterial CreateFlatMirrorMaterial();
    }
}
