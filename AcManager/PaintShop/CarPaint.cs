using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class CarPaint : TexturePattern {
        public CarPaint() : base(true) {
            LiveryPriority = 1;
        }

        public CarPaint SetDetailsParams(PaintShopDestination detailsTexture, bool supportFlakes = true, int flakesSize = 512,
                bool colorAvailable = true, Color? defaultColor = null, Dictionary<string, CarPaintReplacementSource> replacements = null) {
            FlakesSize = flakesSize;
            SupportsFlakes = supportFlakes && flakesSize > 0;
            ColorAvailable = colorAvailable;
            DetailsTexture = detailsTexture;
            DefaultColor = defaultColor ?? Color.FromRgb(255, 255, 255);
            ColorReplacements = replacements?.If(colorAvailable, x => x.Prepend(new KeyValuePair<string, CarPaintReplacementSource>("Solid Color", null)))
                                             .ToDictionary(x => x.Key, x => x.Value);

            if (ColorReplacements != null) {
                ColorReplacementValue = ColorReplacements.FirstOrDefault();
            } else {
                UpdateFlakesAllowed();
                UpdateColorAllowed();
            }

            return this;
        }

        public int FlakesSize { get; private set; }
        public bool SupportsFlakes { get; private set; }
        public bool ColorAvailable { get; private set; }
        public Color DefaultColor { get; private set; }

        private bool _flakesAllowed;

        public bool FlakesAllowed {
            get => _flakesAllowed;
            set {
                if (Equals(value, _flakesAllowed)) return;
                _flakesAllowed = value;
                OnPropertyChanged();
            }
        }

        private void UpdateFlakesAllowed() {
            FlakesAllowed = SupportsFlakes && ColorReplacementValue.Value == null;
        }

        private bool _colorAllowed;

        public bool ColorAllowed {
            get => _colorAllowed;
            set {
                if (Equals(value, _colorAllowed)) return;
                _colorAllowed = value;
                OnPropertyChanged();
            }
        }

        private void UpdateColorAllowed() {
            ColorAllowed = ColorAvailable && ColorReplacementValue.Value == null || ColorReplacementValue.Value.Colored;
        }

        [CanBeNull]
        public PaintShopDestination DetailsTexture { get; private set; }

        [CanBeNull]
        public Dictionary<string, CarPaintReplacementSource> ColorReplacements { get; private set; }

        public bool HasColorReplacements => ColorReplacements?.Any(x => x.Value != null) == true;

        private KeyValuePair<string, CarPaintReplacementSource> _colorReplacementValue;

        public KeyValuePair<string, CarPaintReplacementSource> ColorReplacementValue {
            get => _colorReplacementValue;
            set {
                if (Equals(value, _colorReplacementValue)) return;
                _colorReplacementValue = value;
                OnPropertyChanged();
                UpdateFlakesAllowed();
                UpdateColorAllowed();
                DetailsAspect?.SetDirty();
            }
        }

        public int? LiveryColorId { get; set; } = 0;

        public override Dictionary<int, Color> LiveryColors => LiveryColorId.HasValue ? new Dictionary<int, Color> {
            [LiveryColorId.Value] = Color
        } : base.LiveryColors;

        public override string DisplayName { get; set; } = "Car paint";

        private Color? _color;

        [JsonProperty("color")]
        public Color Color {
            get => _color ?? DefaultColor;
            set {
                if (Equals(value, _color)) return;
                _color = value;
                OnPropertyChanged();
                RaiseColorChanged(0);
                DetailsAspect?.SetDirty();
            }
        }

        private double _flakes = 0.777;

        [JsonProperty("flakes")]
        public double Flakes {
            get => _flakes;
            set {
                if (Equals(value, _flakes)) return;
                _flakes = value;
                OnPropertyChanged();
                DetailsAspect?.SetDirty();
            }
        }

        public string LiveryStyle { get; internal set; }

        public bool GuessColorsFromPreviews { get; internal set; }

        #region Aspects
        [CanBeNull]
        protected PaintableItemAspect DetailsAspect { get; private set; }

        protected override void OnPatternEnabledChanged() {
            base.OnPatternEnabledChanged();
            if (Equals(DetailsTexture, PatternTexture)) {
                DetailsAspect?.SetDirty();
            }
        }

        protected override void Initialize() {
            if (DetailsTexture != null) {
                DetailsAspect = RegisterAspect(DetailsTexture, GetDetailsOverride)
                        .Subscribe(ColorReplacements?.Values.Select(x => x?.Source));
            }

            // Set it below so txDiffuse will be processed after txDetails
            base.Initialize();
        }

        private PaintShopOverrideBase GetDetailsOverride(PaintShopDestination name) {
            var value = ColorReplacementValue.Value;
            if (value != null) {
                if (value.Colored) {
                    return new PaintShopOverrideTint {
                        Colors = new[] { Color.ToColor() },
                        Alpha = ValueAdjustment.Same,
                        Source = value.Source
                    };
                }

                return new PaintShopOverrideWithTexture {
                    Source = value.Source
                };
            }

            var color = ColorAvailable ? Color.ToColor() : DefaultColor.ToColor();
            if (SupportsFlakes && Flakes > 0d) {
                return new PaintShopOverrideWithColor {
                    Color = color,
                    Flakes = Flakes,
                    Size = FlakesSize
                };
            }

            return new PaintShopOverrideWithColor {
                Color = color
            };
        }
        #endregion

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result == null) return null;

            if (ColorReplacementValue.Value != null) {
                result["colorReplacementValue"] = PaintShop.NameToId(ColorReplacementValue.Key, false);
            }

            return result;
        }

        public override void Deserialize(JObject data) {
            base.Deserialize(data);
            if (data == null) return;

            if (HasColorReplacements) {
                var loaded = data["colorReplacementValue"]?.ToString();
                var value = ColorReplacements?.FirstOrDefault(x => PaintShop.NameToId(x.Key, false) == loaded);
                if (value?.Value != null) {
                    ColorReplacementValue = value.Value;
                }
            }
        }

        private void UpdateIsNumberActive() {
            IsNumberActive = CurrentPattern?.HasNumbers == true;
        }

        public override Color? GetColor(int colorIndex) {
            if (ColorAllowed) {
                if (colorIndex == 0) return Color;
                colorIndex--;
            }

            return CurrentPattern?.Colors.ActualColors.ElementAtOrDefault(colorIndex);
        }
    }
}