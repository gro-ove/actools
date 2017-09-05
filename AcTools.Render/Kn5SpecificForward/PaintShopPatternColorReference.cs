using System.Drawing;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public struct PaintShopPatternColorReference {
        private readonly Color? _value;
        private readonly int? _id;

        public PaintShopPatternColorReference(Color? value) {
            _value = value;
            _id = null;
        }

        public PaintShopPatternColorReference(int? id) {
            _value = null;
            _id = id;
        }

        [Pure]
        public Color GetValue([NotNull] Color[] colors) {
            var val = _value;
            if (val.HasValue) return val.Value;

            var id = _id;
            if (id.HasValue) {
                var i = id.Value;
                if (i >= 0 && i < colors.Length) {
                    return colors[i];
                }
            }

            return Color.Black;
        }
    }
}