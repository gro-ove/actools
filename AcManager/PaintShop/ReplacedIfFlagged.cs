using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class ReplacedIfFlagged : AspectsPaintableItem {
        public ReplacedIfFlagged(bool inverse, [NotNull] Dictionary<TextureFileName, PaintShopSource> replacements) : base(false) {
            _inverse = inverse;
            _replacements = replacements;
        }

        protected override void Initialize() {
            base.Initialize();
            foreach (var replacement in _replacements) {
                RegisterAspect(replacement.Key, Apply, Save).Subscribe(replacement.Value);
            }
        }

        private void Apply(TextureFileName name, IPaintShopRenderer renderer) {
            renderer.OverrideTexture(name.FileName, _replacements.GetValueOrDefault(name));
        }

        private Task Save(string location, TextureFileName name, IPaintShopRenderer renderer) {
            var value = _replacements.GetValueOrDefault(name);
            return value == null ? Task.Delay(0) : renderer.SaveTextureAsync(name.FileName, name.PreferredFormat, value);
        }

        public override string DisplayName { get; set; } = "Replaced If Enabled";

        private readonly bool _inverse;
        private readonly Dictionary<TextureFileName, PaintShopSource> _replacements;

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        public override Color? GetColor(int colorIndex) {
            return null;
        }
    }
}