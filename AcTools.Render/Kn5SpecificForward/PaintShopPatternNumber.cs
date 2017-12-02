using JetBrains.Annotations;
using SlimDX.DirectWrite;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopPatternNumber : PaintShopPatternPiece {
        public PaintShopPatternNumber(double size, double left, double top, PaintShopAlignment horizontalAlignment, PaintShopAlignment verticalAlignment,
                [NotNull] PaintShopFontSource font, double angle, PaintShopPatternColorReference colorRef, FontWeight weight, FontStyle style,
                FontStretch stretch, int kerningAdjustment) : base(size, left, top, angle) {
            KerningAdjustment = kerningAdjustment;
            HorizontalAlignment = horizontalAlignment;
            VerticalAlignment = verticalAlignment;
            Font = font;
            ColorRef = colorRef;
            Weight = weight;
            Style = style;
            Stretch = stretch;
        }

        public readonly PaintShopAlignment HorizontalAlignment;
        public readonly PaintShopAlignment VerticalAlignment;

        [NotNull]
        public readonly PaintShopFontSource Font;

        public readonly FontWeight Weight;
        public readonly FontStyle Style;
        public readonly FontStretch Stretch;
        public readonly int KerningAdjustment;
        public readonly PaintShopPatternColorReference ColorRef;

        public int GetFontHashCode() {
            unchecked {
                var hashCode = Size.GetHashCode();
                hashCode = (hashCode * 397) ^ (Font.Filename?.GetHashCode() ?? Font.FamilyName.GetHashCode());
                hashCode = (hashCode * 397) ^ (int)Weight;
                hashCode = (hashCode * 397) ^ (int)Style;
                hashCode = (hashCode * 397) ^ (int)Stretch;
                hashCode = (hashCode * 397) ^ KerningAdjustment.GetHashCode();
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