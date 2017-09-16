using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class ReplacedIfFlagged : PaintableItem {
        public ReplacedIfFlagged(bool inverse, [NotNull] Dictionary<TextureFileName, PaintShopSource> replacements) : base(false) {
            _inverse = inverse;
            _replacements = replacements;
            AffectedTextures.AddRange(_replacements.Keys.Select(x => x.FileName));
        }

        public override string DisplayName { get; set; } = "Replaced If Enabled";

        private readonly bool _inverse;
        private readonly Dictionary<TextureFileName, PaintShopSource> _replacements;

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            foreach (var replacement in _replacements) {
                renderer.OverrideTexture(replacement.Key.FileName, replacement.Value);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var replacement in _replacements) {
                renderer.OverrideTexture(replacement.Key.FileName, null);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            foreach (var replacement in _replacements) {
                if (replacement.Value.Data != null) {
                    await FileUtils.WriteAllBytesAsync(Path.Combine(location, replacement.Key.FileName), replacement.Value.Data);
                } else if (replacement.Value.Name != null) {
                    await renderer.SaveTextureAsync(replacement.Key.FileName, replacement.Key.PreferredFormat, replacement.Value);
                }

                if (cancellation.IsCancellationRequested) return;
            }
        }
    }
}