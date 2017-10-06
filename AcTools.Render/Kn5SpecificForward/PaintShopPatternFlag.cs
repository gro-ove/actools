namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopPatternFlag : PaintShopPatternPiece {
        public readonly double AspectMultiplier;

        public PaintShopPatternFlag(double size, double left, double top, double angle, double aspectMultiplier) : base(size, left, top, angle) {
            AspectMultiplier = aspectMultiplier;
        }
    }
}