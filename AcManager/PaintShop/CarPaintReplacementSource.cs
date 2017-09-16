using AcTools.Render.Kn5SpecificForward;

namespace AcManager.PaintShop {
    public class CarPaintReplacementSource {
        public CarPaintReplacementSource(PaintShopSource source, bool colored) {
            Source = source;
            Colored = colored;
        }

        public PaintShopSource Source { get; }
        public bool Colored { get; }
    }
}