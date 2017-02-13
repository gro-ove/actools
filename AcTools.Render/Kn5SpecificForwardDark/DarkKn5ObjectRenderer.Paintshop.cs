using System.Drawing;
using AcTools.Render.Kn5SpecificForward;
using ImageMagick;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : IPaintShopRenderer {
        public override bool OverrideTextureFlakes(string textureName, Color color) {
            using (var image = new MagickImage(new MagickColor(color) { A = 250 }, 256, 256)) {
                image.AddNoise(NoiseType.Poisson, Channels.Alpha);
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Png));
            }
        }
    }
}