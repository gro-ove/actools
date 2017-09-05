using JetBrains.Annotations;
using SlimDX.DirectWrite;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificForward {
    public enum PaintShopAlignment {
        Start, Center, End
    }

    public sealed class PaintShopPatternNumbers {

        public PaintShopPatternNumbers(double size, double left, double top, PaintShopAlignment horizontalAlignment, PaintShopAlignment verticalAlignment,
                [NotNull] PaintShopFontSource font, double angle, PaintShopPatternColorReference colorRef, FontWeight weight, FontStyle style,
                FontStretch stretch) {
            Size = size;
            Left = left;
            Top = top;
            HorizontalAlignment = horizontalAlignment;
            VerticalAlignment = verticalAlignment;
            Font = font;
            Angle = angle;
            ColorRef = colorRef;
            Weight = weight;
            Style = style;
            Stretch = stretch;
        }

        public readonly double Size;
        public readonly double Left;
        public readonly double Top;
        public readonly double Angle;
        public readonly PaintShopAlignment HorizontalAlignment;
        public readonly PaintShopAlignment VerticalAlignment;

        [NotNull]
        public readonly PaintShopFontSource Font;

        public readonly FontWeight Weight;
        public readonly FontStyle Style;
        public readonly FontStretch Stretch;
        public readonly PaintShopPatternColorReference ColorRef;

        public int GetFontHashCode() {
            unchecked {
                var hashCode = Size.GetHashCode();
                hashCode = (hashCode * 397) ^ (Font.Filename?.GetHashCode() ?? Font.FamilyName.GetHashCode());
                hashCode = (hashCode * 397) ^ (int)Weight;
                hashCode = (hashCode * 397) ^ (int)Style;
                hashCode = (hashCode * 397) ^ (int)Stretch;
                return hashCode;
            }
        }

        public TextAlignment GetTextAlignment() {
            var h = HorizontalAlignment;
            var v = VerticalAlignment;
            return (h == PaintShopAlignment.Start ? TextAlignment.Left : h == PaintShopAlignment.End ? TextAlignment.Right : TextAlignment.HorizontalCenter) |
                    (v == PaintShopAlignment.Start ? TextAlignment.Top : v == PaintShopAlignment.End ? TextAlignment.Bottom : TextAlignment.VerticalCenter);
        }
    }
}