using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Materials {
    public class Kn5SharedMaterials : SharedMaterials {
        private readonly Kn5 _kn5;

        public Kn5SharedMaterials(IDeviceContextHolder holder, Kn5 kn5) : base(holder.Get<IMaterialsFactory>()) {
            _kn5 = kn5;
        }

        protected override IRenderableMaterial CreateMaterial(object key) {
            if (key is uint) {
                var id = (uint)key;
                return base.CreateMaterial(new Kn5MaterialDescription(_kn5.GetMaterial(id)));
            }

            var special = key as Tuple<object, uint>;
            if (special != null) {
                return base.CreateMaterial(new Kn5MaterialDescription(special.Item1, _kn5.GetMaterial(special.Item2)));
            }

            return base.CreateMaterial(key);
        }
    }

    public sealed class Kn5MaterialDescription {
        public object SpecialKey { get; }

        [CanBeNull]
        public Kn5Material Material { get; }

        public Kn5MaterialDescription(object specialKey, [CanBeNull] Kn5Material material) {
            SpecialKey = specialKey;
            Material = material;
        }

        public Kn5MaterialDescription([CanBeNull] Kn5Material material) {
            Material = material;
        }

        private bool Equals(Kn5MaterialDescription other) {
            return Equals(Material, other.Material);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var a = obj as Kn5MaterialDescription;
            return a != null && Equals(a);
        }

        public override int GetHashCode() {
            return Material?.GetHashCode() ?? 0;
        }
    }

    public sealed class Kn5AmbientShadowMaterialDescription {
        [NotNull]
        public string Filename { get; }

        public Kn5AmbientShadowMaterialDescription(string filename) {
            Filename = filename;
        }

        private bool Equals(Kn5AmbientShadowMaterialDescription other) {
            return string.Equals(Filename, other.Filename);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var a = obj as Kn5AmbientShadowMaterialDescription;
            return a != null && Equals(a);
        }

        public override int GetHashCode() {
            return Filename.GetHashCode();
        }
    }
}