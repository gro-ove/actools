using JetBrains.Annotations;
using SlimDX.DirectWrite;

namespace AcTools.Render.Kn5SpecificForward {
    public sealed class PaintShopPatternLabel : PaintShopPatternNumber {
        public PaintShopPatternLabel(double size, double left, double top, PaintShopAlignment horizontalAlignment, PaintShopAlignment verticalAlignment,
                [NotNull] PaintShopFontSource font, double angle, PaintShopPatternColorReference colorRef, FontWeight weight, FontStyle style,
                FontStretch stretch, [CanBeNull] string role)
                : base(size, left, top, horizontalAlignment, verticalAlignment, font, angle, colorRef, weight, style, stretch) {
            Role = role ?? "pilot";
        }

        [NotNull]
        public readonly string Role;
    }
}