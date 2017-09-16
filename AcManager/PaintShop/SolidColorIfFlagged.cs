using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class SolidColorIfFlagged : PaintableItem {
        public SolidColorIfFlagged([NotNull] TextureFileName[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
            _textures = textures;
            _inverse = inverse;
            _color = color;
            _opacity = opacity;
            AffectedTextures.AddRange(textures.Select(x => x.FileName));
        }

        private readonly TextureFileName[] _textures;
        private readonly bool _inverse;
        private readonly Color _color;
        private readonly double _opacity;

        public override string DisplayName { get; set; } = "Colored If Enabled";

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            foreach (var texture in _textures) {
                renderer.OverrideTexture(texture.FileName, _color.ToColor(), _opacity);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var texture in _textures) {
                renderer.OverrideTexture(texture.FileName, null);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            foreach (var texture in _textures) {
                await renderer.SaveTextureAsync(Path.Combine(location, texture.FileName), texture.PreferredFormat, _color.ToColor(), _opacity);
                if (cancellation.IsCancellationRequested) return;
            }
        }
    }
}