using AcTools.Kn5File;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Materials {
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
            return obj is Kn5MaterialDescription a && Equals(a);
        }

        public override int GetHashCode() {
            return Material?.GetHashCode() ?? 0;
        }
    }
}