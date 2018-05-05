using System;
using System.Collections.Generic;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Base.Materials {
    public class SharedMaterials : IDisposable {
        private readonly IMaterialsFactory _provider;

        public SharedMaterials(IMaterialsFactory provider) {
            _provider = provider;
        }

        [NotNull]
        protected virtual IRenderableMaterial CreateMaterial(object key) {
            return _provider.CreateMaterial(key);
        }

        private readonly Dictionary<int, IRenderableMaterial> _materials = new Dictionary<int, IRenderableMaterial>();

        [NotNull]
        public IRenderableMaterial GetMaterial([NotNull] object key) {
            var keyHash = key.GetHashCode();
            if (_materials.TryGetValue(keyHash, out var material)) return material;
            return _materials[keyHash] = CreateMaterial(key);
        }

        public void Dispose() {
            _materials.DisposeEverything();
        }
    }
}
