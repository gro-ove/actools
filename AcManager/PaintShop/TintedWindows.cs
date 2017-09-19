using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using Newtonsoft.Json;

namespace AcManager.PaintShop {
    // TODO: Unite with ColoredItem?
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

        protected override void Initialize() {
            foreach (var replacement in Replacements) {
                RegisterAspect(replacement.Key, GetApply(replacement.Value), GetSave(replacement.Value));
            }
        }

        private Action<TextureFileName, IPaintShopRenderer> GetApply(PaintShop.TintedEntry v) {
            return (name, renderer) => renderer.OverrideTextureTint(name.FileName, Colors.DrawingColors, Alpha, v.Source, v.Mask, v.Overlay);
        }

        private Func<string, TextureFileName, IPaintShopRenderer, Task> GetSave(PaintShop.TintedEntry v) {
            return (location, name, renderer) => renderer.SaveTextureTintAsync(Path.Combine(location, name.FileName), name.PreferredFormat,
                    Colors.DrawingColors, Alpha, v.Source, v.Mask, v.Overlay);
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