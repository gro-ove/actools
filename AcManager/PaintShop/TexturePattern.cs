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
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class TexturePattern : PaintableItem, IPaintableNumberItem {
        private int _patternNumber;

        [JsonProperty("patternNumber")]
        public int PatternNumber {
            get => _patternNumber;
            set {
                value = value.Clamp(0, 9999);
                if (Equals(value, _patternNumber)) return;
                _patternNumber = value;
                OnPropertyChanged();
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
            }
        }

        public ChangeableObservableCollection<CarPaintPattern> Patterns { get; }

        public TexturePattern(TextureFileName patternTexture, PaintShopSource patternBase, [CanBeNull] PaintShopSource patternOverlay,
                IEnumerable<CarPaintPattern> patterns) : base(true) {
            PatternTexture = patternTexture;
            PatternBase = patternBase;
            PatternOverlay = patternOverlay;
            Patterns = new ChangeableObservableCollection<CarPaintPattern>(patterns);
            Patterns.ItemPropertyChanged += OnPatternChanged;
            CurrentPattern = Patterns[0];
            AffectedTextures.Add(patternTexture.FileName);
        }

        private void OnPatternChanged(object sender, PropertyChangedEventArgs e) {
            Update();
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            if (PatternTexture == null) return;
            if (CurrentPattern != null) {
                renderer.OverrideTexturePattern(PatternTexture.FileName, PatternBase ?? PaintShopSource.InputSource, CurrentPattern.Source,
                        CurrentPattern.Overlay ?? PatternOverlay, CurrentPattern.Colors.DrawingColors, PatternNumber, CurrentPattern.Numbers,
                        CurrentPattern.Size);
            } else {
                renderer.OverrideTexture(PatternTexture.FileName, null);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            if (PatternTexture == null) return;
            renderer.OverrideTexture(PatternTexture.FileName, null);
        }

        protected override Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            return PatternTexture != null && CurrentPattern != null
                    ? renderer.SaveTexturePatternAsync(Path.Combine(location, PatternTexture.FileName), PatternTexture.PreferredFormat,
                            PatternBase ?? PaintShopSource.InputSource, CurrentPattern.Source, CurrentPattern.Overlay ?? PatternOverlay,
                            CurrentPattern.Colors.DrawingColors, PatternNumber, CurrentPattern.Numbers, CurrentPattern.Size)
                    : Task.Delay(0);
        }

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result == null) return null;

            if (PatternTexture != null) {
                result["patternSelected"] = CurrentPattern?.DisplayName;
                result["patternColors"] = SerializeColors(CurrentPattern?.Colors);
            }

            return result;
        }

        public override void Deserialize(JObject data) {
            base.Deserialize(data);

            if (data != null && PatternTexture != null) {
                var current = data.GetStringValueOnly("patternSelected");
                CurrentPattern = Patterns.FirstOrDefault(x => String.Equals(x.DisplayName, current, StringComparison.OrdinalIgnoreCase)) ?? CurrentPattern;
                if (CurrentPattern != null) {
                    DeserializeColors(CurrentPattern.Colors, data, "patternColors");
                }
            }
        }

        // which color is in which slot, −1 if there is no color in given slot
        [CanBeNull]
        public int[] LiveryColorIds { get; set; }

        public override Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
            Slot = i,
            Color = x == -1 ? (Color?)null : CurrentPattern?.Colors.Colors.ElementAtOrDefault(x)?.Value
        }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? base.LiveryColors;

        int IPaintableNumberItem.Number {
            set => PatternNumber = value;
        }

        public bool IsNumberActive { get; set; }

        private void UpdateIsNumberActive() {
            IsNumberActive = CurrentPattern?.HasNumbers == true;
        }
    }
}