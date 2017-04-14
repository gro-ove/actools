using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Controls.CustomShowroom {
    public static partial class PaintShop {
        [JsonObject(MemberSerialization.OptIn)]
        public abstract class PaintableItem : Displayable, IDisposable {
            protected PaintableItem(bool enabledByDefault) {
                _enabled = enabledByDefault;
            }
            
            [CanBeNull]
            private IPaintShopRenderer _renderer;

            public void SetRenderer(IPaintShopRenderer renderer) {
                _renderer = renderer;
            }

            private bool _enabled;

            [JsonProperty("enabled")]
            public bool Enabled {
                get { return _enabled; }
                set {
                    if (Equals(value, _enabled)) return;
                    _enabled = value;
                    OnEnabledChanged();
                    OnPropertyChanged();
                }
            }

            private bool _guessed;

            public bool Guessed {
                get { return _guessed; }
                internal set {
                    if (Equals(value, _guessed)) return;
                    _guessed = value;
                    OnPropertyChanged();
                }
            }

            protected virtual void OnEnabledChanged() {}

            private bool _updating;

            protected async void Update() {
                if (_updating) return;

                try {
                    _updating = true;
                    await Task.Delay(10);
                    if (_updating && !_disposed) {
                        UpdateOverride();
                    }
                } finally {
                    _updating = false;
                }
            }

            protected override void OnPropertyChanged(string propertyName = null) {
                base.OnPropertyChanged(propertyName);
                Update();
            }

            protected virtual bool IsActive() {
                return Enabled;
            }

            protected void UpdateOverride() {
                var renderer = _renderer;
                if (renderer == null) return;

                if (IsActive()) {
                    ApplyOverride(renderer);
                } else {
                    ResetOverride(renderer);
                }
            }

            protected abstract void ApplyOverride([NotNull] IPaintShopRenderer renderer);

            protected abstract void ResetOverride([NotNull] IPaintShopRenderer renderer);

            [NotNull]
            protected abstract Task SaveOverrideAsync([NotNull] IPaintShopRenderer renderer, string location);

            [NotNull]
            public Task SaveAsync(string location) {
                var renderer = _renderer;
                if (renderer == null) return Task.Delay(0);

                return IsActive() ? SaveOverrideAsync(renderer, location) : Task.Delay(0);
            }

            private bool _disposed;

            public virtual void Dispose() {
                if (_renderer != null) {
                    ResetOverride(_renderer);
                }

                _disposed = true;
            }

            [CanBeNull]
            public virtual JObject Serialize() {
                return JObject.FromObject(this);
            }

            public virtual void Deserialize([CanBeNull] JObject data) {
                data?.Populate(this);
            }
        }

        public class SolidColorIfFlagged : PaintableItem {
            public SolidColorIfFlagged(string[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
                _textures = textures;
                _inverse = inverse;
                _color = color;
                _opacity = opacity;
            }

            private readonly string[] _textures;
            private readonly bool _inverse;
            private readonly Color _color;
            private readonly double _opacity;

            public override string DisplayName { get; set; } = "Colored If Enabled";

            protected override bool IsActive() {
                return Enabled ^ _inverse;
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var texture in _textures) {
                    renderer.OverrideTexture(texture, _color.ToColor(), _opacity);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var texture in _textures) {
                    renderer.OverrideTexture(texture, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var texture in _textures) {
                    await renderer.SaveTextureAsync(Path.Combine(location, texture), _color.ToColor(), _opacity);
                }
            }
        }

        public class TransparentIfFlagged : SolidColorIfFlagged {
            public TransparentIfFlagged(string[] textures, bool inverse) : base(textures, inverse, Colors.Black, 0d) { }

            public override string DisplayName { get; set; } = "Transparent If Enabled";
        }

        public class ReplacedIfFlagged : PaintableItem {
            public ReplacedIfFlagged(bool inverse, Dictionary<string, PaintShopSource> replacements) : base(false) {
                _inverse = inverse;
                _replacements = replacements;
            }

            public override string DisplayName { get; set; } = "Replaced If Enabled";
            
            private readonly bool _inverse;
            private readonly Dictionary<string, PaintShopSource> _replacements;

            protected override bool IsActive() {
                return Enabled ^ _inverse;
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in _replacements) {
                    renderer.OverrideTexture(replacement.Key, replacement.Value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in _replacements) {
                    renderer.OverrideTexture(replacement.Key, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var replacement in _replacements) {
                    if (replacement.Value.Data != null) {
                        await FileUtils.WriteAllBytesAsync(Path.Combine(location, replacement.Key), replacement.Value.Data);
                    } else if (replacement.Value.Name != null) {
                        await renderer.SaveTextureAsync(replacement.Key, replacement.Value);
                    }
                }
            }
        }

        public class ColoredItem : PaintableItem {
            protected readonly Dictionary<string, PaintShopSource> Replacements;

            public ColoredItem([Localizable(false)] string diffuseTexture, Color defaultColor) : base(false) {
                Replacements = new Dictionary<string, PaintShopSource> {
                    [diffuseTexture] = PaintShopSource.White
                };

                DefaultColor = defaultColor;
            }

            public ColoredItem(Dictionary<string, PaintShopSource> replacements, Color defaultColor) : base(false) {
                Replacements = replacements;
                DefaultColor = defaultColor;
            }

            public bool AutoAdjustLevels { get; internal set; }

            public Color DefaultColor { get; }

            public override string DisplayName { get; set; } = "Colored item";

            private Color? _color;

            [JsonProperty("color")]
            public Color Color {
                get { return _color ?? DefaultColor; }
                set {
                    if (Equals(value, _color)) return;
                    _color = value;
                    OnPropertyChanged();
                }
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTextureTint(replacement.Key, Color.ToColor(), AutoAdjustLevels, 0d, replacement.Value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTexture(replacement.Key, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var replacement in Replacements) {
                    await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key), Color.ToColor(), AutoAdjustLevels, 0d, replacement.Value);
                }
            }
        }

        public class TintedWindows : ColoredItem { 
            public double DefaultAlpha { get; set; }

            public TintedWindows([Localizable(false)] string diffuseTexture, double defaultAlpha = 0.23, Color? defaultColor = null)
                    : base(diffuseTexture, defaultColor ?? Color.FromRgb(41, 52, 55)) {
                DefaultAlpha = defaultAlpha;
            }

            public TintedWindows(Dictionary<string, PaintShopSource> replacements, double defaultAlpha = 0.23, Color? defaultColor = null)
                    : base(replacements, defaultColor ?? Color.FromRgb(41, 52, 55)) {
                DefaultAlpha = defaultAlpha;
            }

            public override string DisplayName { get; set; } = "Windows";

            private double? _alpha;

            [JsonProperty("alpha")]
            public double Alpha {
                get { return _alpha ?? DefaultAlpha; }
                set {
                    if (Equals(value, _alpha)) return;
                    _alpha = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Transparency));
                }
            }

            public double Transparency {
                get { return 1d - Alpha; }
                set { Alpha = 1d - value; }
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTextureTint(replacement.Key, Color.ToColor(), AutoAdjustLevels, Alpha, replacement.Value);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var replacement in Replacements) {
                    await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key), Color.ToColor(), AutoAdjustLevels, Alpha,
                            replacement.Value);
                }
            }
        }

        public sealed class CarPaintPatternColor : Displayable {
            public CarPaintPatternColor(string name, Color defaultValue) {
                DisplayName = name;
                Value = defaultValue;
            }

            private Color _value;

            public Color Value {
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public sealed class CarPaintPattern : Displayable {
            public CarPaintPattern(string name, PaintShopSource source, [CanBeNull] PaintShopSource overlay, CarPaintPatternColor[] colors) {
                DisplayName = name;
                Source = source;
                Overlay = overlay;
                Colors = colors;

                foreach (var color in Colors) {
                    color.PropertyChanged += OnColorChanged;
                }

                ActualColors = new Color[Colors.Length];
                DrawingColors = new System.Drawing.Color[Colors.Length];
                UpdateActualColors();
            }

            public CarPaintPattern(string name, PaintShopSource source, [CanBeNull] PaintShopSource overlay, Color[] colors)
                    : this(name, source, overlay,  colors.Select((x, i) => new CarPaintPatternColor($"Color #{i + 1}", x)).ToArray()) { }

            public CarPaintPattern(string name, PaintShopSource source, [CanBeNull] PaintShopSource overlay, int colors)
                    : this(name, source, overlay,
                            Enumerable.Range(1, colors).Select(x => new CarPaintPatternColor($"Color #{x}", System.Windows.Media.Colors.White)).ToArray()) {}

            private void OnColorChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(nameof(Colors));
                UpdateActualColors();
            }

            public PaintShopSource Source { get; }

            [CanBeNull]
            public PaintShopSource Overlay { get; }

            public CarPaintPatternColor[] Colors { get; }

            private void UpdateActualColors() {
                for (var i = 0; i < Colors.Length; i++) {
                    ActualColors[i] = Colors[i].Value;
                    DrawingColors[i] = ActualColors[i].ToColor();
                }
            }

            public readonly Color[] ActualColors;
            public readonly System.Drawing.Color[] DrawingColors;
        }

        public class CarPaint : PaintableItem {
            public string DetailsTexture { get; }

            public Color DefaultColor { get; }

            public CarPaint(string detailsTexture, Color? defaultColor = null) : base(true) {
                DetailsTexture = detailsTexture;
                DefaultColor = defaultColor ?? Color.FromRgb(255, 255, 255);
                Patterns = new ChangeableObservableCollection<CarPaintPattern>();
                Patterns.ItemPropertyChanged += OnPatternChanged;
            }

            public bool SupportsFlakes { get; internal set; } = true;

            public override string DisplayName { get; set; } = "Car paint";

            private Color? _color;

            [JsonProperty("color")]
            public Color Color {
                get { return _color ?? DefaultColor; }
                set {
                    if (Equals(value, _color)) return;
                    _color = value;
                    OnPropertyChanged();
                }
            }

            private double _flakes = 0.3d;

            [JsonProperty("flakes")]
            public double Flakes {
                get { return _flakes; }
                set {
                    if (Equals(value, _flakes)) return;
                    _flakes = value;
                    OnPropertyChanged();
                }
            }

            public string PatternTexture { get; private set; }

            public PaintShopSource PatternBase { get; private set; }

            public PaintShopSource PatternOverlay { get; private set; }

            private CarPaintPattern _currentPattern;

            public CarPaintPattern CurrentPattern {
                get { return _currentPattern; }
                set {
                    if (Equals(value, _currentPattern)) return;
                    _currentPattern = value;
                    OnPropertyChanged();
                    _patternChanged = true;
                }
            }

            public ChangeableObservableCollection<CarPaintPattern> Patterns { get; }

            public bool AoAutoAdjustLevels { get; internal set; }

            public void SetPatterns(string patternTexture, PaintShopSource patternBase, [CanBeNull] PaintShopSource patternOverlay,
                    IEnumerable<CarPaintPattern> patterns) {
                PatternTexture = patternTexture;
                PatternBase = patternBase;
                PatternOverlay = patternOverlay;
                Patterns.ReplaceEverythingBy(patterns.Prepend(new CarPaintPattern("Nothing", PaintShopSource.White, null, 0)));
                CurrentPattern = Patterns[0];
                _patternChanged = true;
            }

            private void OnPatternChanged(object sender, PropertyChangedEventArgs e) {
                Update();
                _patternChanged = true;
            }

            private bool _patternEnabled = true;

            public bool PatternEnabled {
                get { return _patternEnabled; }
                set {
                    if (Equals(value, _patternEnabled)) return;
                    _patternEnabled = value;
                    OnPropertyChanged();
                    _patternChanged = true;
                }
            }

            private Color? _previousColor;
            private double? _previousFlakes;
            private bool _patternChanged;

            protected override void OnEnabledChanged() {
                _previousColor = null;
            }

            protected void ApplyColor(IPaintShopRenderer renderer) {
                if (_previousColor != Color || _previousFlakes != Flakes) {
                    _previousColor = Color;
                    _previousFlakes = Flakes;

                    if (SupportsFlakes && Flakes > 0d) {
                        renderer.OverrideTextureFlakes(DetailsTexture, Color.ToColor(), Flakes);
                    } else {
                        renderer.OverrideTexture(DetailsTexture, Color.ToColor(), 1d);
                    }
                }
            }

            protected void ApplyPattern(IPaintShopRenderer renderer) {
                if (!_patternChanged || PatternTexture == null) return;
                _patternChanged = false;

                if (PatternEnabled && CurrentPattern != null) {
                    renderer.OverrideTexturePattern(PatternTexture, PatternBase ?? PaintShopSource.InputSource, AoAutoAdjustLevels,
                            CurrentPattern.Source, CurrentPattern.Overlay ?? PatternOverlay, CurrentPattern.DrawingColors);
                } else {
                    renderer.OverrideTexture(PatternTexture, null);
                }
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                ApplyColor(renderer);
                ApplyPattern(renderer);
            }

            protected Task SaveColorAsync(IPaintShopRenderer renderer, string location) {
                return SupportsFlakes && Flakes > 0d
                        ? renderer.SaveTextureFlakesAsync(Path.Combine(location, DetailsTexture), Color.ToColor(), Flakes)
                        : renderer.SaveTextureAsync(Path.Combine(location, DetailsTexture), Color.ToColor(), 1d);
            }

            protected Task SavePatternAsync(IPaintShopRenderer renderer, string location) {
                return PatternEnabled && PatternTexture != null && CurrentPattern != null
                        ? renderer.SaveTexturePatternAsync(Path.Combine(location, PatternTexture), PatternBase ?? PaintShopSource.InputSource,
                                AoAutoAdjustLevels, CurrentPattern.Source, CurrentPattern.Overlay ?? PatternOverlay,
                                CurrentPattern.DrawingColors)
                        : Task.Delay(0);
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                await SaveColorAsync(renderer, location);
                await SavePatternAsync(renderer, location);
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                renderer.OverrideTexture(DetailsTexture, null);
                if (PatternTexture != null) {
                    renderer.OverrideTexture(PatternTexture, null);
                }
            }

            public override JObject Serialize() {
                var result = base.Serialize();
                if (result == null) return null;

                if (PatternTexture != null) {
                    result["patternEnabled"] = PatternEnabled;
                    result["patternSelected"] = CurrentPattern?.DisplayName;
                    result["patternColors"] = CurrentPattern == null ? null :
                            JArray.FromObject(CurrentPattern.ActualColors.Select(x => x.ToHexString()));
                }

                return result;
            }

            public override void Deserialize(JObject data) {
                base.Deserialize(data);
                
                if (data != null && PatternTexture != null) {
                    PatternEnabled = data.GetBoolValueOnly("patternEnabled") != false;
                    var current = data.GetStringValueOnly("patternSelected");
                    CurrentPattern = Patterns.FirstOrDefault(x => string.Equals(x.DisplayName, current, StringComparison.OrdinalIgnoreCase)) ?? CurrentPattern;
                    if (CurrentPattern != null) {
                        var colors = (data["patternColors"] as JArray)?.ToObject<string[]>();
                        if (colors != null) {
                            for (var i = 0; i < colors.Length && i < CurrentPattern.Colors.Length; i++) {
                                CurrentPattern.Colors[i].Value = colors[i].ToColor() ?? Colors.White;
                            }
                        }
                    }
                }
            }
        }

        public class ComplexCarPaint : CarPaint {
            public string MapsTexture { get; }

            [CanBeNull, Localizable(false)]
            public PaintShopSource MapsDefaultTexture { get; internal set; }

            public bool AutoAdjustLevels { get; internal set; }

            public bool FixGloss { get; internal set; }

            public ComplexCarPaint([Localizable(false)] string detailsTexture, [Localizable(false)] string mapsTexture, Color? defaultColor = null)
                    : base(detailsTexture, defaultColor) {
                MapsTexture = mapsTexture;
            }

            private bool _complexMode;
            private bool? _previousComplexMode;

            [JsonProperty("complex")]
            public bool ComplexMode {
                get { return _complexMode; }
                set {
                    if (Equals(value, _complexMode)) return;
                    _complexMode = value;
                    OnPropertyChanged();
                }
            }

            private double _reflection = 1d;
            private double _previousReflection = -1d;

            [JsonProperty("reflection")]
            public double Reflection {
                get { return _reflection; }
                set {
                    if (Equals(value, _reflection)) return;
                    _reflection = value;
                    OnPropertyChanged();
                }
            }

            private double _gloss = 1d;
            private double _previousGloss = -1d;

            [JsonProperty("gloss")]
            public double Gloss {
                get { return _gloss; }
                set {
                    if (Equals(value, _gloss)) return;
                    _gloss = value;
                    OnPropertyChanged();
                }
            }

            private double _specular = 1d;
            private double _previousSpecular = -1d;

            [JsonProperty("specular")]
            public double Specular {
                get { return _specular; }
                set {
                    if (Equals(value, _specular)) return;
                    _specular = value;
                    OnPropertyChanged();
                }
            }

            protected override void OnEnabledChanged() {
                base.OnEnabledChanged();
                _previousGloss = _previousReflection = _previousSpecular = -1d;
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                base.ApplyOverride(renderer);
                if (_previousComplexMode != _complexMode || Math.Abs(_previousReflection - Reflection) > 0.001 || Math.Abs(_previousGloss - Gloss) > 0.001 ||
                        Math.Abs(_previousSpecular - Specular) > 0.001) {
                    if (ComplexMode) {
                        renderer.OverrideTextureMaps(MapsTexture, Reflection, Gloss, Specular, AutoAdjustLevels, FixGloss,
                                MapsDefaultTexture ?? PaintShopSource.InputSource);
                    } else {
                        renderer.OverrideTexture(MapsTexture, null);
                    }

                    _previousComplexMode = _complexMode;
                    _previousReflection = Reflection;
                    _previousGloss = Gloss;
                    _previousSpecular = Specular;
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                base.ResetOverride(renderer);
                renderer.OverrideTexture(MapsTexture, null);
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                await base.SaveOverrideAsync(renderer, location);
                if (ComplexMode) {
                    await renderer.SaveTextureMapsAsync(Path.Combine(location, MapsTexture), Reflection, Gloss, Specular, AutoAdjustLevels, FixGloss,
                            MapsDefaultTexture ?? new PaintShopSource(MapsTexture));
                }
            }
        }
    }
}