using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class SolidColorIfFlagged : AspectsPaintableItem {
        public SolidColorIfFlagged([NotNull] PaintShopDestination[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
            _textures = textures;
            _inverse = inverse;
            _color = color;
            _opacity = opacity;
        }

        protected override void Initialize() {
            foreach (var texture in _textures) {
                RegisterAspect(texture, GetOverride);
            }
        }

        private PaintShopOverrideBase GetOverride(PaintShopDestination name) {
            return new PaintShopOverrideWithColor {
                Color = _color.ToColor((_opacity * 255).ClampToByte())
            };
        }

        private readonly PaintShopDestination[] _textures;
        private readonly bool _inverse;
        private readonly Color _color;
        private readonly double _opacity;

        public override string DisplayName { get; set; } = "Colored if enabled";

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        public override Color? GetColor(int colorIndex) {
            return _color;
        }
    }
}