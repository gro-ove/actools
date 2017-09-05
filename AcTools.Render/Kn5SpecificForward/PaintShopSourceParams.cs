namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSourceParams {
        public bool Desaturate { get; set; }
        public bool NormalizeMax { get; set; }
        public bool RequiresPreparation => Desaturate || NormalizeMax;
    }
}