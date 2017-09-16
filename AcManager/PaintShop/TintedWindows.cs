using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using Newtonsoft.Json;

namespace AcManager.PaintShop {
    public class TintedWindows : ColoredItem {
        public double DefaultAlpha { get; set; }

        public bool FixedColor { get; set; }

        public TintedWindows([Localizable(false)] TextureFileName diffuseTexture, Color? defaultColor = null, double defaultAlpha = 0.23,
                bool fixedColor = false) : base(diffuseTexture, defaultColor ?? Color.FromRgb(41, 52, 55)) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        public TintedWindows([Localizable(false)] TextureFileName diffuseTexture, CarPaintColors colors, double defaultAlpha = 0.23, bool fixedColor = false)
                : base(diffuseTexture, colors) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        public TintedWindows(Dictionary<TextureFileName, PaintShop.TintedEntry> replacements, CarPaintColors colors, double defaultAlpha = 0.23,
                bool fixedColor = false) : base(replacements, colors) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        public override string DisplayName { get; set; } = "Windows";

        private double? _alpha;

        [JsonProperty("alpha")]
        public double Alpha {
            get => _alpha ?? DefaultAlpha;
            set {
                if (Equals(value, _alpha)) return;
                _alpha = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Transparency));
            }
        }

        public double Transparency {
            get => 1d - Alpha;
            set => Alpha = 1d - value;
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            foreach (var replacement in Replacements) {
                renderer.OverrideTextureTint(replacement.Key.FileName, Colors.DrawingColors, Alpha,
                        replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            foreach (var replacement in Replacements) {
                await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key.FileName), replacement.Key.PreferredFormat, Colors.DrawingColors,
                        Alpha, replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                if (cancellation.IsCancellationRequested) return;
            }
        }
    }
}