namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopPatternDecal : PaintShopPatternFlag {
        public PaintShopSource Source;
        public readonly PaintShopPatternColorReference ColorRef;

        public PaintShopPatternDecal(double size, double left, double top, double angle, double aspectMultiplier, PaintShopPatternColorReference colorRef)
                : base(size, left, top, angle, aspectMultiplier) {
            ColorRef = colorRef;
        }
    }
}