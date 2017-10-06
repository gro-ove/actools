namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSourceParams {
        public bool Desaturate;
        public bool NormalizeMax;
        public bool RequiresPreparation => Desaturate || NormalizeMax;

        public ValueAdjustment RedAdjustment = ValueAdjustment.Same,
                GreenAdjustment = ValueAdjustment.Same,
                BlueAdjustment = ValueAdjustment.Same,
                AlphaAdjustment = ValueAdjustment.Same;

        public bool AnyChannelAdjusted => RedAdjustment != ValueAdjustment.Same || GreenAdjustment != ValueAdjustment.Same ||
                BlueAdjustment != ValueAdjustment.Same || AlphaAdjustment != ValueAdjustment.Same;

        public PaintShopSourceParams AdjustChannels(ValueAdjustment red, ValueAdjustment green, ValueAdjustment blue, ValueAdjustment alpha) {
            RedAdjustment = red;
            GreenAdjustment = green;
            BlueAdjustment = blue;
            AlphaAdjustment = alpha;
            return this;
        }

        public override string ToString() {
            return $"(desat={Desaturate}, norm={NormalizeMax}, ra={RedAdjustment}, ga={GreenAdjustment}, ba={BlueAdjustment}, aa={AlphaAdjustment})";
        }
    }
}