using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class TexturePattern : AspectsPaintableItem, IPaintablePersonalItem {
        protected TexturePattern(bool enabledByDefault) : base(enabledByDefault) {
            Patterns = new ChangeableObservableCollection<CarPaintPattern>();
            Patterns.ItemPropertyChanged += OnPatternChanged;
        }

        public TexturePattern() : this(false) { }

        private int _patternNumber;

        [CanBeNull]
        private string _patternFlagTexture;

        [CanBeNull]
        private IReadOnlyDictionary<string, string> _patternLabels;

        public PaintShopDestination PatternTexture { get; private set; }
        public PaintShopSource PatternBase { get; private set; }
        public PaintShopSource PatternOverlay { get; private set; }
        public PaintShopSource PatternUnderlay { get; private set; }

        private CarPaintPattern _currentPattern;

        [CanBeNull]
        public CarPaintPattern CurrentPattern {
            get => _currentPattern;
            set {
                if (Equals(value, _currentPattern)) return;
                _currentPattern = value;
                OnPropertyChanged();
                OnCurrentPatternChanged();
            }
        }

        public ChangeableObservableCollection<CarPaintPattern> Patterns { get; }

        public TexturePattern SetPatterns(PaintShopDestination patternTexture, [CanBeNull] PaintShopSource patternBase,
                [CanBeNull] PaintShopSource patternOverlay, [CanBeNull] PaintShopSource patternUnderlay,
                IEnumerable<CarPaintPattern> patterns, bool forcePattern) {
            PatternTexture = patternTexture;
            PatternBase = patternBase;
            PatternOverlay = patternOverlay;
            PatternUnderlay = patternUnderlay;
            Patterns.ReplaceEverythingBy(forcePattern ? patterns : patterns.Prepend(CarPaintPattern.Nothing));
            CurrentPattern = Patterns[0];
            return this;
        }

        protected virtual void OnCurrentPatternChanged() {
            UpdateIsNumberActive();
            PatternAspect?.SetDirty();
        }

        protected virtual void OnPatternChanged(object sender, PropertyChangedEventArgs e) {
            PatternAspect?.SetDirty();
            if (e.PropertyName == nameof(CarPaintPattern.ActualColors)) {
                RaiseColorChanged(null);
            }
        }

        private bool _patternEnabled = true;

        public bool PatternEnabled {
            get => _patternEnabled;
            set {
                if (Equals(value, _patternEnabled)) return;
                _patternEnabled = value;
                OnPropertyChanged();

                if (PatternAspect != null) {
                    PatternAspect.IsEnabled = value;
                }

                OnPatternEnabledChanged();
            }
        }

        public Color BackgroundColorHint { get; set; } = Colors.Transparent;

        [Pure]
        protected virtual Color GetBackgroundColorHint() {
            return BackgroundColorHint;
        }

        protected virtual void OnPatternEnabledChanged() { }

        [CanBeNull]
        protected PaintableItemAspect PatternAspect { get; private set; }

        protected override void Initialize() {
            if (PatternTexture != null) {
                var patternAspect = RegisterAspect(PatternTexture,
                        name => CurrentPattern != null ? new PaintShopOverridePattern {
                            Ao = PatternBase ?? PaintShopSource.InputSource,
                            Pattern = CurrentPattern.Source,
                            Overlay = CurrentPattern.Overlay ?? PatternOverlay,
                            Underlay = CurrentPattern.Underlay ?? PatternUnderlay,
                            Colors = CurrentPattern.Colors.DrawingColors,
                            BackgroundColorHint = GetBackgroundColorHint().ToColor(),
                            Numbers = CurrentPattern.Numbers,
                            Flags = CurrentPattern.Flags,
                            Labels = CurrentPattern.Labels,
                            Decals = CurrentPattern.Decals,
                            SkinNumber = _patternNumber,
                            SkinFlagFilename = _patternFlagTexture,
                            SkinLabels = _patternLabels,
                            Destination = new PaintShopDestination(name.TextureName, name.PreferredFormat, name.OutputMask, CurrentPattern.Size)
                                    .InheritExtendedPropertiesFrom(name)
                        } : null,
                        PatternEnabled)
                        .Subscribe(PatternBase, PatternOverlay, PatternUnderlay);
                PatternAspect = patternAspect;
                foreach (var pattern in Patterns) {
                    patternAspect.Subscribe(new[] { pattern.Source, pattern.Overlay, pattern.Underlay }, c => CurrentPattern == pattern);
                }
            }
        }

        public override Color? GetColor(int colorIndex) {
            return CurrentPattern?.Colors.ActualColors.ElementAtOrDefault(colorIndex);
        }

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result == null) return null;

            if (PatternTexture != null) {
                result["patternEnabled"] = PatternEnabled;
                result["patternSelected"] = CurrentPattern?.DisplayName;
                result["patternColors"] = SerializeColors(CurrentPattern?.Colors);
            }

            return result;
        }

        public override void Deserialize(JObject data) {
            base.Deserialize(data);

            if (data != null && PatternTexture != null) {
                PatternEnabled = data.GetBoolValueOnly("patternEnabled") != false;
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

        int IPaintablePersonalItem.Number {
            set {
                _patternNumber = value;
                PatternAspect?.SetDirty();
            }
        }

        string IPaintablePersonalItem.FlagTexture {
            set {
                _patternFlagTexture = value;
                PatternAspect?.SetDirty();
            }
        }

        IReadOnlyDictionary<string, string> IPaintablePersonalItem.Labels {
            set {
                _patternLabels = value;
                PatternAspect?.SetDirty();
            }
        }

        private bool _isNumberActive;

        public bool IsNumberActive {
            get => _isNumberActive;
            set {
                if (value == _isNumberActive) return;
                _isNumberActive = value;
                OnPropertyChanged();
            }
        }

        private bool _isFlagActive;

        public bool IsFlagActive {
            get => _isFlagActive;
            set {
                if (value == _isFlagActive) return;
                _isFlagActive = value;
                OnPropertyChanged();
            }
        }

        private IReadOnlyList<string> _activeLabels;

        public IReadOnlyList<string> ActiveLabels {
            get => _activeLabels;
            set {
                if (Equals(value, _activeLabels)) return;
                _activeLabels = value;
                OnPropertyChanged();
            }
        }

        private void UpdateIsNumberActive() {
            IsNumberActive = CurrentPattern?.HasNumbers == true;
            IsFlagActive = CurrentPattern?.HasFlags == true;
            ActiveLabels = CurrentPattern?.LabelRoles;
        }
    }
}