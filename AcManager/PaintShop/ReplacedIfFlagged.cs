using System.Collections.Generic;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class ReplacedIfFlagged : AspectsPaintableItem {
        public ReplacedIfFlagged(bool inverse, [NotNull] Dictionary<PaintShopDestination, PaintShopSource> replacements) : base(false) {
            _inverse = inverse;
            _replacements = replacements;
        }

        protected override void Initialize() {
            base.Initialize();
            foreach (var replacement in _replacements) {
                RegisterAspect(replacement.Key, GetOverride).Subscribe(replacement.Value);
            }
        }

        private PaintShopOverrideBase GetOverride(PaintShopDestination name) {
            return new PaintShopOverrideWithTexture {
                Source = _replacements.GetValueOrDefault(name)
            };
        }

        public override string DisplayName { get; set; } = "Replaced if enabled";

        private readonly bool _inverse;
        private readonly Dictionary<PaintShopDestination, PaintShopSource> _replacements;

        protected override bool IsActive() {
            return Enabled ^ _inverse;
        }

        public override Color? GetColor(int colorIndex) {
            return null;
        }
    }
}