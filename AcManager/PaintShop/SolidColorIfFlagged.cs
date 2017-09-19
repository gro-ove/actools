using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class SolidColorIfFlagged : AspectsPaintableItem {
        public SolidColorIfFlagged([NotNull] TextureFileName[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
            _textures = textures;
            _inverse = inverse;
            _color = color;
            _opacity = opacity;
        }

        protected override void Initialize() {
            foreach (var texture in _textures) {
                RegisterAspect(texture, Apply, Save);
            }
        }

        private void Apply(TextureFileName name, IPaintShopRenderer renderer) {
            renderer.OverrideTexture(name.FileName, _color.ToColor(), _opacity);
        }

        private Task Save(string location, TextureFileName name, IPaintShopRenderer renderer) {
            return renderer.SaveTextureAsync(Path.Combine(location, name.FileName), name.PreferredFormat, _color.ToColor(), _opacity);
        }

        private readonly TextureFileName[] _textures;
        private readonly bool _inverse;
        private readonly Color _color;
        private readonly double _opacity;

        public override string DisplayName { get; set; } = "Colored If Enabled";

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        public override Color? GetColor(int colorIndex) {
            return _color;
        }
    }
}