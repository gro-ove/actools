using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Materials {
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
            return obj is Kn5AmbientShadowMaterialDescription a && Equals(a);
        }

        public override int GetHashCode() {
            return Filename.GetHashCode();
        }
    }
}