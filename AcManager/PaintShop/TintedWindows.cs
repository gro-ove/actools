using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.PaintShop {
    public class TintedWindows : ColoredItem {
        public double DefaultAlpha { get; set; }

        public bool FixedColor { get; set; }

        public TintedWindows([Localizable(false)] PaintShopDestination diffuseTexture, Color? defaultColor = null, double defaultAlpha = 0.23,
                bool fixedColor = false) : base(diffuseTexture, defaultColor ?? Color.FromRgb(41, 52, 55)) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        public TintedWindows([Localizable(false)] PaintShopDestination diffuseTexture, CarPaintColors colors, double defaultAlpha = 0.23, bool fixedColor = false)
                : base(diffuseTexture, colors) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        public TintedWindows(Dictionary<PaintShopDestination, PaintShop.TintedEntry> replacements, CarPaintColors colors, double defaultAlpha = 0.23,
                bool fixedColor = false) : base(replacements, colors) {
            DefaultAlpha = defaultAlpha;
            FixedColor = fixedColor;
        }

        protected override void Initialize() {
            foreach (var replacement in Replacements) {
                RegisterAspect(replacement.Key, name => {
                    var v = Replacements.GetValueOrDefault(name);
                    return new PaintShopOverrideTint {
                        Colors = Colors.DrawingColors,
                        Alpha = new ValueAdjustment((float)Alpha, 1f),
                        Source = v?.Source,
                        Mask = v?.Mask,
                        Overlay = v?.Overlay
                    };
                });
            }
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
    }
}