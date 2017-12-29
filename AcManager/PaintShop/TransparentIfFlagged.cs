using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class TransparentIfFlagged : SolidColorIfFlagged {
        public TransparentIfFlagged([NotNull] PaintShopDestination[] textures, bool inverse) : base(textures, inverse, Colors.Black, 0d) { }

        public override string DisplayName { get; set; } = "Transparent if enabled";
    }
}