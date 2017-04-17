using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools;
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
            protected int UpdateDelay = 10;

            protected async void Update() {
                if (_updating) return;

                try {
                    _updating = true;

                    if (IsActive()) {
                        await Task.Delay(UpdateDelay);
                    }

                    if (_updating && !_disposed) {
                        UpdateOverride();
                    }
                } finally {
                    _updating = false;
                }
            }

            protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
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

            public List<string> AffectedTextures { get; } = new List<string>(5);

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

            [CanBeNull]
            protected JArray SerializeColors([CanBeNull] CarPaintColors target) {
                return target == null ? null : JArray.FromObject(target.ActualColors.Select(x => x.ToHexString()));
            }

            public virtual void Deserialize([CanBeNull] JObject data) {
                data?.Populate(this);
            }

            protected void DeserializeColors([NotNull] CarPaintColors target, JObject data, string key) {
                var colors = (data?[key] as JArray)?.ToObject<string[]>();
                if (colors != null) {
                    for (var i = 0; i < colors.Length && i < target.Colors.Length; i++) {
                        target.Colors[i].Value = colors[i].ToColor() ?? Colors.White;
                    }
                }
            }
        }

        public class SolidColorIfFlagged : PaintableItem {
            public SolidColorIfFlagged([NotNull] string[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
                _textures = textures;
                _inverse = inverse;
                _color = color;
                _opacity = opacity;
                AffectedTextures.AddRange(textures);
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
            public TransparentIfFlagged([NotNull] string[] textures, bool inverse) : base(textures, inverse, Colors.Black, 0d) { }

            public override string DisplayName { get; set; } = "Transparent If Enabled";
        }

        public class ReplacedIfFlagged : PaintableItem {
            public ReplacedIfFlagged(bool inverse, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
                _inverse = inverse;
                _replacements = replacements;
                AffectedTextures.AddRange(_replacements.Keys);
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

        public class Replacement : PaintableItem {
            [NotNull]
            private readonly string[] _textures;

            public Dictionary<string, PaintShopSource> Replacements { get; }

            public Replacement([NotNull] string[] textures, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
                _textures = textures;
                Replacements = replacements;
                Value = Replacements.FirstOrDefault();
                AffectedTextures.AddRange(_textures);
            }

            private KeyValuePair<string, PaintShopSource> _value;

            public KeyValuePair<string, PaintShopSource> Value {
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var tex in _textures) {
                    renderer.OverrideTexture(tex, value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var tex in _textures) {
                    renderer.OverrideTexture(tex, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var tex in _textures) {
                    await renderer.SaveTextureAsync(Path.Combine(location, tex), value);
                }
            }

            public override JObject Serialize() {
                var result = base.Serialize();
                if (result != null) {
                    result["value"] = NameToId(Value.Key, false);
                }

                return result;
            }

            public override void Deserialize(JObject data) {
                base.Deserialize(data);
                if (data != null) {
                    var loaded = data["value"]?.ToString();
                    var value = Replacements.FirstOrDefault(x => NameToId(x.Key, false) == loaded);
                    if (value.Value != null) {
                        Value = value;
                    }
                }
            }
        }

        public class MultiReplacement : PaintableItem {
            public Dictionary<string, Dictionary<string, PaintShopSource>> Replacements { get; }

            public MultiReplacement(Dictionary<string, Dictionary<string, PaintShopSource>> replacements) : base(false) {
                Replacements = replacements;
                Value = Replacements.FirstOrDefault();
                AffectedTextures.AddRange(Replacements.Values.SelectMany(x => x.Keys));
            }

            private KeyValuePair<string, Dictionary<string, PaintShopSource>> _value;

            public KeyValuePair<string, Dictionary<string, PaintShopSource>> Value {
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var pair in value) {
                    renderer.OverrideTexture(pair.Key, pair.Value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var tex in AffectedTextures) {
                    renderer.OverrideTexture(tex, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var pair in value) {
                    await renderer.SaveTextureAsync(Path.Combine(location, pair.Key), pair.Value);
                }
            }

            public override JObject Serialize() {
                var result = base.Serialize();
                if (result != null) {
                    result["value"] = NameToId(Value.Key, false);
                }

                return result;
            }

            public override void Deserialize(JObject data) {
                base.Deserialize(data);
                if (data != null) {
                    var loaded = data["value"]?.ToString();
                    var value = Replacements.FirstOrDefault(x => NameToId(x.Key, false) == loaded);
                    if (value.Value != null) {
                        Value = value;
                    }
                }
            }
        }

        public class ColoredItem : PaintableItem {
            protected readonly Dictionary<string, TintedEntry> Replacements;

            public ColoredItem([Localizable(false)] string diffuseTexture, Color defaultColor)
                    : this(diffuseTexture, new CarPaintColors(defaultColor)) {}

            public ColoredItem([Localizable(false)] string diffuseTexture, CarPaintColors colors)
                    : this(new Dictionary<string, TintedEntry> {
                        [diffuseTexture] = new TintedEntry(PaintShopSource.White, null, null)
                    }, colors) {}

            public ColoredItem(Dictionary<string, TintedEntry> replacements, CarPaintColors colors) : base(false) {
                Replacements = replacements;
                Colors = colors;
                Colors.PropertyChanged += OnColorsChanged;
                AffectedTextures.AddRange(Replacements.Keys);
            }

            public override string DisplayName { get; set; } = "Colored item";

            public CarPaintColors Colors { get; }

            public Color[] ActualColors => Colors.ActualColors;

            private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(nameof(ActualColors));
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTextureTint(replacement.Key, Colors.DrawingColors, 0d,
                            replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTexture(replacement.Key, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var replacement in Replacements) {
                    await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key), Colors.DrawingColors, 0d,
                            replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                }
            }

            public override JObject Serialize() {
                var result = base.Serialize();
                if (result != null) {
                    result["colors"] = SerializeColors(Colors);
                }

                return result;
            }

            public override void Deserialize(JObject data) {
                base.Deserialize(data);
                if (data != null) {
                    DeserializeColors(Colors, data, "colors");
                }
            }
        }

        public class TintedWindows : ColoredItem { 
            public double DefaultAlpha { get; set; }

            public bool FixedColor { get; set; }

            public TintedWindows([Localizable(false)] string diffuseTexture, Color? defaultColor = null, double defaultAlpha = 0.23, bool fixedColor = false)
                    : base(diffuseTexture, defaultColor ?? Color.FromRgb(41, 52, 55)) {
                DefaultAlpha = defaultAlpha;
                FixedColor = fixedColor;
            }

            public TintedWindows([Localizable(false)] string diffuseTexture, CarPaintColors colors, double defaultAlpha = 0.23, bool fixedColor = false)
                    : base(diffuseTexture, colors) {
                DefaultAlpha = defaultAlpha;
                FixedColor = fixedColor;
            }

            public TintedWindows(Dictionary<string, TintedEntry> replacements, CarPaintColors colors, double defaultAlpha = 0.23, bool fixedColor = false) 
                    : base(replacements, colors) {
                DefaultAlpha = defaultAlpha;
                FixedColor = fixedColor;
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
                    renderer.OverrideTextureTint(replacement.Key, Colors.DrawingColors, Alpha,
                            replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location) {
                foreach (var replacement in Replacements) {
                    await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key), Colors.DrawingColors, Alpha,
                            replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                }
            }
        }

        public sealed class CarPaintColor : Displayable {
            public CarPaintColor(string name, Color defaultValue, [CanBeNull] Dictionary<string, Color> allowedValues) {
                AllowedValues = allowedValues?.Count == 0 ? null : allowedValues;
                DisplayName = name;
                Value = defaultValue;
            }

            private Color _value;

            public Color Value {
                get { return _value; }
                set {
                    if (AllowedValues != null && !AllowedValues.ContainsValue(value)) {
                        value = AllowedValues.First().Value;
                    }

                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ValuePair));
                }
            }

            public KeyValuePair<string, Color> ValuePair {
                get {
                    return AllowedValues?.FirstOrDefault(x => x.Value == _value) ?? new KeyValuePair<string, Color>(ToolsStrings.Common_None, Colors.Transparent);
                }
                set { Value = value.Value; }
            }

            [CanBeNull]
            public Dictionary<string, Color> AllowedValues { get; }
        }

        public class CarPaintColors : NotifyPropertyChanged {
            public CarPaintColors(CarPaintColor[] colors) {
                Colors = colors;
                
                ActualColors = new Color[Colors.Length];
                DrawingColors = new System.Drawing.Color[Colors.Length];
                UpdateActualColors();

                foreach (var color in Colors) {
                    color.PropertyChanged += OnColorChanged;
                }
            }

            public CarPaintColors() : this(new CarPaintColor[0]) { }

            public CarPaintColors(Color defaultColor) : this(new[] {
                new CarPaintColor("Color", defaultColor, null),
            }) {}

            public CarPaintColor[] Colors { get; }

            private void OnColorChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(nameof(Colors));
                UpdateActualColors();
            }

            private void UpdateActualColors() {
                for (var i = 0; i < Colors.Length; i++) {
                    ActualColors[i] = Colors[i].Value;
                    DrawingColors[i] = ActualColors[i].ToColor();
                }
            }

            public readonly Color[] ActualColors;
            public readonly System.Drawing.Color[] DrawingColors;
        }

        public sealed class CarPaintPattern : Displayable {
            public CarPaintPattern(string name, [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource overlay, CarPaintColors colors) {
                DisplayName = name;
                Source = source;
                Overlay = overlay;
                Colors = colors;
                colors.PropertyChanged += OnColorsChanged;
            }

            public Color[] ActualColors => Colors.ActualColors;

            private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(nameof(ActualColors));
            }

            [NotNull]
            public PaintShopSource Source { get; }

            [CanBeNull]
            public PaintShopSource Overlay { get; }

            [NotNull]
            public CarPaintColors Colors { get; }
        }

        public class CarPaint : PaintableItem {
            public string DetailsTexture { get; }

            public Color DefaultColor { get; }

            public CarPaint(string detailsTexture, int flakesSize = 256, Color? defaultColor = null) : base(true) {
                FlakesSize = flakesSize;
                SupportsFlakes = flakesSize > 0;
                DetailsTexture = detailsTexture;
                DefaultColor = defaultColor ?? Color.FromRgb(255, 255, 255);
                Patterns = new ChangeableObservableCollection<CarPaintPattern>();
                Patterns.ItemPropertyChanged += OnPatternChanged;
                AffectedTextures.Add(detailsTexture);
            }

            public bool SupportsFlakes { get; }

            public int FlakesSize { get; }

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

            public void SetPatterns(string patternTexture, PaintShopSource patternBase, [CanBeNull] PaintShopSource patternOverlay,
                    IEnumerable<CarPaintPattern> patterns) {
                PatternTexture = patternTexture;
                PatternBase = patternBase;
                PatternOverlay = patternOverlay;
                Patterns.ReplaceEverythingBy(patterns.Prepend(new CarPaintPattern("Nothing", PaintShopSource.Transparent, null, new CarPaintColors())));
                CurrentPattern = Patterns[0];
                _patternChanged = true;
                AffectedTextures.Add(patternTexture);
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

            public string LiveryStyle { get; internal set; }

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
                        renderer.OverrideTextureFlakes(DetailsTexture, Color.ToColor(), FlakesSize, Flakes);
                    } else {
                        renderer.OverrideTexture(DetailsTexture, Color.ToColor(), 1d);
                    }
                }
            }

            protected void ApplyPattern(IPaintShopRenderer renderer) {
                if (!_patternChanged || PatternTexture == null) return;
                _patternChanged = false;

                if (PatternEnabled && CurrentPattern != null) {
                    renderer.OverrideTexturePattern(PatternTexture, PatternBase ?? PaintShopSource.InputSource, CurrentPattern.Source,
                            CurrentPattern.Overlay ?? PatternOverlay, CurrentPattern.Colors.DrawingColors);
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
                        ? renderer.SaveTextureFlakesAsync(Path.Combine(location, DetailsTexture), Color.ToColor(), FlakesSize, Flakes)
                        : renderer.SaveTextureAsync(Path.Combine(location, DetailsTexture), Color.ToColor(), 1d);
            }

            protected Task SavePatternAsync(IPaintShopRenderer renderer, string location) {
                return PatternEnabled && PatternTexture != null && CurrentPattern != null
                        ? renderer.SaveTexturePatternAsync(Path.Combine(location, PatternTexture), PatternBase ?? PaintShopSource.InputSource,
                                CurrentPattern.Source, CurrentPattern.Overlay ?? PatternOverlay, CurrentPattern.Colors.DrawingColors)
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
                    result["patternColors"] = SerializeColors(CurrentPattern?.Colors);
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
                        DeserializeColors(CurrentPattern.Colors, data, "patternColors");
                    }
                }
            }
        }

        public class ComplexCarPaint : CarPaint {
            public string MapsTexture { get; }

            [NotNull]
            public PaintShopSource MapsDefaultTexture { get; }

            public bool FixGloss { get; internal set; }

            public ComplexCarPaint([Localizable(false)] string detailsTexture, int flakesSize, [Localizable(false)] string mapsTexture,
                    [NotNull] PaintShopSource mapsSource, Color? defaultColor = null) : base(detailsTexture, flakesSize, defaultColor) {
                MapsTexture = mapsTexture;
                MapsDefaultTexture = mapsSource;
                AffectedTextures.Add(mapsTexture);
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
                        renderer.OverrideTextureMaps(MapsTexture, Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture);
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
                    await renderer.SaveTextureMapsAsync(Path.Combine(location, MapsTexture), Reflection, Gloss, Specular, FixGloss, MapsDefaultTexture);
                }
            }
        }
    }
}