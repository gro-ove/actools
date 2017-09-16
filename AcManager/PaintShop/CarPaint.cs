using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class CarPaint : PaintableItem, IPaintableNumberItem {
        public CarPaint() : base(true) {
            LiveryPriority = 1;
            Patterns = new ChangeableObservableCollection<CarPaintPattern>();
            Patterns.ItemPropertyChanged += OnPatternChanged;
        }

        public CarPaint SetDetailsParams(TextureFileName detailsTexture, bool supportFlakes = true, int flakesSize = 512,
                bool colorAvailable = true, Color? defaultColor = null,
                Dictionary<string, CarPaintReplacementSource> replacements = null) {
            FlakesSize = flakesSize;
            SupportsFlakes = supportFlakes && flakesSize > 0;
            ColorAvailable = colorAvailable;
            DetailsTexture = detailsTexture;
            DefaultColor = defaultColor ?? Color.FromRgb(255, 255, 255);
            ColorReplacements = replacements?.Prepend(new KeyValuePair<string, CarPaintReplacementSource>("Solid Color", null))
                                             .ToDictionary(x => x.Key, x => x.Value);

            if (ColorReplacements != null) {
                ColorReplacementValue = ColorReplacements.FirstOrDefault();
            } else {
                UpdateFlakesAllowed();
                UpdateColorAllowed();
            }

            AffectedTextures.Add(detailsTexture.FileName);
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
            ColorAllowed = ColorAvailable && (ColorReplacementValue.Value == null || ColorReplacementValue.Value.Colored);
        }

        [CanBeNull]
        public TextureFileName DetailsTexture { get; private set; }

        [CanBeNull]
        public Dictionary<string, CarPaintReplacementSource> ColorReplacements { get; private set; }

        public bool HasColorReplacements => ColorReplacements != null;

        private KeyValuePair<string, CarPaintReplacementSource> _colorReplacementValue;

        public KeyValuePair<string, CarPaintReplacementSource> ColorReplacementValue {
            get => _colorReplacementValue;
            set {
                if (Equals(value, _colorReplacementValue)) return;
                _colorReplacementValue = value;
                OnPropertyChanged();
                UpdateFlakesAllowed();
                UpdateColorAllowed();
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
            }
        }

        private int _patternNumber;

        [JsonProperty("patternNumber")]
        public int PatternNumber {
            get => _patternNumber;
            set {
                value = value.Clamp(0, 9999);
                if (Equals(value, _patternNumber)) return;
                _patternNumber = value;
                OnPropertyChanged();
                _patternChanged = true;
            }
        }

        public TextureFileName PatternTexture { get; private set; }
        public PaintShopSource PatternBase { get; private set; }
        public PaintShopSource PatternOverlay { get; private set; }

        private CarPaintPattern _currentPattern;

        [CanBeNull]
        public CarPaintPattern CurrentPattern {
            get => _currentPattern;
            set {
                if (Equals(value, _currentPattern)) return;
                _currentPattern = value;
                OnPropertyChanged();
                UpdateIsNumberActive();
                _patternChanged = true;
            }
        }

        public ChangeableObservableCollection<CarPaintPattern> Patterns { get; }

        public CarPaint SetPatterns(TextureFileName patternTexture, PaintShopSource patternBase, [CanBeNull] PaintShopSource patternOverlay,
                IEnumerable<CarPaintPattern> patterns) {
            PatternTexture = patternTexture;
            PatternBase = patternBase;
            PatternOverlay = patternOverlay;
            Patterns.ReplaceEverythingBy(patterns.Prepend(CarPaintPattern.Nothing));
            CurrentPattern = Patterns[0];
            _patternChanged = true;
            AffectedTextures.Add(patternTexture.FileName);
            return this;
        }

        private void OnPatternChanged(object sender, PropertyChangedEventArgs e) {
            Update();
            _patternChanged = true;
        }

        private bool _patternEnabled = true;

        public bool PatternEnabled {
            get => _patternEnabled;
            set {
                if (Equals(value, _patternEnabled)) return;
                _patternEnabled = value;
                OnPropertyChanged();
                _patternChanged = true;
            }
        }

        public string LiveryStyle { get; internal set; }

        public bool GuessColorsFromPreviews { get; internal set; }

        private Color? _previousColor;
        private double? _previousFlakes;
        private string _previousColorReplacement;
        private bool _patternChanged;

        protected override void OnEnabledChanged() {
            _previousColor = null;
            _patternChanged = true;
        }

        protected void ApplyColor(IPaintShopRenderer renderer) {
            var details = DetailsTexture;
            if (details == null) return;

            if (_previousColor != Color || _previousFlakes != Flakes || _previousColorReplacement != ColorReplacementValue.Key) {
                _previousColor = Color;
                _previousFlakes = Flakes;
                _previousColorReplacement = ColorReplacementValue.Key;

                var value = ColorReplacementValue.Value;
                if (value != null) {
                    if (value.Colored) {
                        renderer.OverrideTextureTint(details.FileName, new[] { Color.ToColor() }, 0d, value.Source, null, null);
                    } else {
                        renderer.OverrideTexture(details.FileName, value.Source);
                    }
                } else {
                    var color = ColorAvailable ? Color.ToColor() : DefaultColor.ToColor();
                    if (SupportsFlakes && Flakes > 0d) {
                        renderer.OverrideTextureFlakes(details.FileName, color, FlakesSize, Flakes);
                    } else {
                        renderer.OverrideTexture(details.FileName, color, 1d);
                    }
                }
            }
        }

        protected void ApplyPattern(IPaintShopRenderer renderer) {
            if (!_patternChanged || PatternTexture == null) return;
            _patternChanged = false;

            if (PatternEnabled && CurrentPattern != null) {
                renderer.OverrideTexturePattern(PatternTexture.FileName, PatternBase ?? PaintShopSource.InputSource, CurrentPattern.Source,
                        CurrentPattern.Overlay ?? PatternOverlay, CurrentPattern.Colors.DrawingColors, PatternNumber, CurrentPattern.Numbers,
                        CurrentPattern.Size);
            } else {
                renderer.OverrideTexture(PatternTexture.FileName, null);
            }
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            ApplyColor(renderer);
            ApplyPattern(renderer);
        }

        protected Task SaveColorAsync(IPaintShopRenderer renderer, string location) {
            var details = DetailsTexture;
            if (details == null) return Task.Delay(0);

            var value = ColorReplacementValue.Value;
            if (value != null) {
                return value.Colored
                        ? renderer.SaveTextureTintAsync(Path.Combine(location, details.FileName), details.PreferredFormat, new[] { Color.ToColor() }, 0d,
                                value.Source, null, null)
                        : renderer.SaveTextureAsync(Path.Combine(location, details.FileName), details.PreferredFormat, value.Source);
            }

            var color = ColorAvailable ? Color.ToColor() : DefaultColor.ToColor();
            return SupportsFlakes && Flakes > 0d
                    ? renderer.SaveTextureFlakesAsync(Path.Combine(location, details.FileName), details.PreferredFormat, color,
                            FlakesSize, Flakes)
                    : renderer.SaveTextureAsync(Path.Combine(location, details.FileName), details.PreferredFormat, color, 1d);
        }

        protected Task SavePatternAsync(IPaintShopRenderer renderer, string location) {
            return PatternEnabled && PatternTexture != null && CurrentPattern != null
                    ? renderer.SaveTexturePatternAsync(Path.Combine(location, PatternTexture.FileName), PatternTexture.PreferredFormat,
                            PatternBase ?? PaintShopSource.InputSource, CurrentPattern.Source, CurrentPattern.Overlay ?? PatternOverlay,
                            CurrentPattern.Colors.DrawingColors, PatternNumber, CurrentPattern.Numbers, CurrentPattern.Size)
                    : Task.Delay(0);
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            await SaveColorAsync(renderer, location);
            if (cancellation.IsCancellationRequested) return;
            await SavePatternAsync(renderer, location);
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            if (DetailsTexture != null) {
                renderer.OverrideTexture(DetailsTexture.FileName, null);
            }
            if (PatternTexture != null) {
                renderer.OverrideTexture(PatternTexture.FileName, null);
            }
        }

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result == null) return null;

            if (ColorReplacementValue.Value != null) {
                result["colorReplacementValue"] = PaintShop.NameToId(ColorReplacementValue.Key, false);
            }

            if (PatternTexture != null) {
                result["patternEnabled"] = PatternEnabled;
                result["patternSelected"] = CurrentPattern?.DisplayName;
                result["patternColors"] = SerializeColors(CurrentPattern?.Colors);
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

            if (PatternTexture != null) {
                PatternEnabled = data.GetBoolValueOnly("patternEnabled") != false;
                var current = data.GetStringValueOnly("patternSelected");
                CurrentPattern = Patterns.FirstOrDefault(x => String.Equals(x.DisplayName, current, StringComparison.OrdinalIgnoreCase)) ?? CurrentPattern;
                if (CurrentPattern != null) {
                    DeserializeColors(CurrentPattern.Colors, data, "patternColors");
                }
            }
        }

        int IPaintableNumberItem.Number {
            set => PatternNumber = value;
        }

        public bool IsNumberActive { get; set; }

        private void UpdateIsNumberActive() {
            IsNumberActive = CurrentPattern?.HasNumbers == true;
        }
    }
}