namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopPatternPiece {
        public PaintShopPatternPiece(double size, double left, double top, double angle) {
            Size = size;
            Left = left;
            Top = top;
            Angle = angle;
        }

        public readonly double Size;
        public readonly double Left;
        public readonly double Top;
        public readonly double Angle;
    }
}