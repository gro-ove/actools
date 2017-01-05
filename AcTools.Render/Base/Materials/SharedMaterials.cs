using System;
using System.Collections.Generic;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Base.Materials {
    public class SharedMaterials : IDisposable {
        private readonly IMaterialsFactory _provider;

        public SharedMaterials(IMaterialsFactory provider) {
            _provider = provider;
        }

        protected virtual IRenderableMaterial CreateMaterial(object key) {
            return _provider.CreateMaterial(key);
        }

        private readonly Dictionary<int, IRenderableMaterial> _materials = new Dictionary<int, IRenderableMaterial>();

        public IRenderableMaterial GetMaterial(object key) {
            var keyHash = key.GetHashCode();

            IRenderableMaterial material;
            if (_materials.TryGetValue(keyHash, out material)) return material;
            return _materials[keyHash] = CreateMaterial(key);
        }

        public void Dispose() {
            _materials.DisposeEverything();
        }
    }
}
