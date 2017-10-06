using System;
using System.Drawing;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public struct PaintShopPatternColorReference {
        private readonly Color? _value;
        private readonly int? _id;

        [CanBeNull]
        private readonly ColorReference _colorRef;

        public PaintShopPatternColorReference(Color? value) {
            _value = value;
            _id = null;
            _colorRef = null;
        }

        public PaintShopPatternColorReference(int? id) {
            _value = null;
            _id = id;
            _colorRef = null;
        }

        public PaintShopPatternColorReference(ColorReference colorRef) {
            _value = null;
            _id = null;
            _colorRef = colorRef;
        }

        public bool IsReference => _colorRef != null;

        public event EventHandler Updated {
            add {
                if (_colorRef != null) {
                    _colorRef.Updated += value;
                }
            }
            remove {
                if (_colorRef != null) {
                    _colorRef.Updated += value;
                }
            }
        }

        [Pure]
        public Color GetValue([CanBeNull] Color[] colors) {
            var r = _colorRef;
            if (r != null) {
                return r.GetValue() ?? Color.Black;
            }

            var val = _value;
            if (val.HasValue) return val.Value;

            var id = _id;
            if (id.HasValue && colors != null) {
                var i = id.Value;
                if (i >= 0 && i < colors.Length) {
                    return colors[i];
                }
            }

            return Color.Black;
        }
    }
}