using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Color = System.Windows.Media.Color;

namespace AcManager.CustomShowroom {
    public static partial class PaintShop {
        public class TextureFileName {
            [NotNull]
            public readonly string FileName;
            public readonly PreferredDdsFormat PreferredFormat;

            public TextureFileName([NotNull] string name, PreferredDdsFormat format) {
                FileName = name ?? throw new ArgumentNullException(nameof(name));
                PreferredFormat = format;
            }

            public TextureFileName([NotNull] string name) {
                if (name == null) throw new ArgumentNullException(nameof(name));
                var index = name.IndexOf(':');
                if (index == -1) {
                    FileName = name;
                    PreferredFormat = PreferredDdsFormat.AutoTransparency;
                } else {
                    FileName = name.Substring(0, index);
                    PreferredFormat = ParseFormat(name.Substring(index + 1));
                }
            }

            private static PreferredDdsFormat ParseFormat(string format) {
                switch (format.Trim().ToLowerInvariant()) {
                    case "dxt1":
                        return PreferredDdsFormat.DXT1;

                    case "dxt":
                    case "dxt5":
                        return PreferredDdsFormat.DXT5;

                    case "l":
                    case "lum":
                    case "luminance":
                        return PreferredDdsFormat.Luminance;

                    case "la":
                    case "lumalpha":
                    case "luminancealpha":
                        return PreferredDdsFormat.LuminanceTransparency;

                    case "rgb565":
                    case "rgb5650":
                    case "565":
                    case "5650":
                        return PreferredDdsFormat.RGB565;

                    case "rgba4444":
                    case "4444":
                        return PreferredDdsFormat.RGBA4444;

                    case "rgba":
                    case "rgba8888":
                    case "8888":
                        return PreferredDdsFormat.NoCompressionTransparency;

                    case "rgb":
                    case "rgb888":
                    case "rgba8880":
                    case "888":
                    case "8880":
                        return PreferredDdsFormat.NoCompression;
                }

                return Enum.TryParse(format, true, out PreferredDdsFormat result) ?
                        result : PreferredDdsFormat.AutoTransparency;
            }

            public override int GetHashCode() {
                return FileName.GetHashCode();
            }

            public override bool Equals(object obj) {
                return FileName.Equals((obj as TextureFileName)?.FileName);
            }
        }

        public interface IPaintableNumberItem {
            int Number { set; }
            bool IsNumberActive { get; }
        }

        /*public interface IPaintableDriverCountryItem {
            int Number { set; }
        }

        public interface IPaintableDriverNameItem {
            int Number { set; }
        }*/

        [JsonObject(MemberSerialization.OptIn)]
        public abstract class PaintableItem : Displayable, IDisposable, IWithId {
            protected PaintableItem(bool enabledByDefault) {
                _enabled = enabledByDefault;
            }

            [CanBeNull]
            private IPaintShopRenderer _renderer;

            public void SetRenderer(IPaintShopRenderer renderer) {
                _renderer = renderer;
                Update();
            }

            [NotNull]
            public virtual Dictionary<int, Color> LiveryColors => new Dictionary<int, Color>(0);

            public int LiveryPriority { get; set; }

            private bool _enabled;

            [JsonProperty("enabled")]
            public bool Enabled {
                get => _enabled;
                set {
                    if (Equals(value, _enabled)) return;
                    _enabled = value;
                    OnEnabledChanged();
                    OnPropertyChanged();
                }
            }

            private bool _guessed;

            public bool Guessed {
                get => _guessed;
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

                if (_renderer != null) {
                    Update();
                }
            }

            protected virtual bool IsActive() {
                return Enabled;
            }

            protected void UpdateOverride() {
                var renderer = _renderer;
                if (renderer == null) return;

                var r = (BaseRenderer)renderer;
                r.IsPaused = true;

                try {
                    if (IsActive()) {
                        ApplyOverride(renderer);
                    } else {
                        ResetOverride(renderer);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    Enabled = false;
                } finally {
                    r.IsPaused = false;
                }
            }

            public List<string> AffectedTextures { get; } = new List<string>(5);

            protected abstract void ApplyOverride([NotNull] IPaintShopRenderer renderer);

            protected abstract void ResetOverride([NotNull] IPaintShopRenderer renderer);

            [NotNull]
            protected abstract Task SaveOverrideAsync([NotNull] IPaintShopRenderer renderer, string location, CancellationToken cancellation);

            [NotNull]
            public async Task SaveAsync(string location, CancellationToken cancellation) {
                var renderer = _renderer;
                if (renderer == null) return;

                try {
                    if (IsActive()) {
                        await SaveOverrideAsync(renderer, location, cancellation);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    Enabled = false;
                }
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

            private string _id;
            public string Id => _id ?? (_id = NameToId(DisplayName, false));
        }

        public class SolidColorIfFlagged : PaintableItem {
            public SolidColorIfFlagged([NotNull] TextureFileName[] textures, bool inverse, Color color, double opacity = 1d) : base(false) {
                _textures = textures;
                _inverse = inverse;
                _color = color;
                _opacity = opacity;
                AffectedTextures.AddRange(textures.Select(x => x.FileName));
            }

            private readonly TextureFileName[] _textures;
            private readonly bool _inverse;
            private readonly Color _color;
            private readonly double _opacity;

            public override string DisplayName { get; set; } = "Colored If Enabled";

            protected override bool IsActive() {
                return Enabled ^ _inverse;
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var texture in _textures) {
                    renderer.OverrideTexture(texture.FileName, _color.ToColor(), _opacity);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var texture in _textures) {
                    renderer.OverrideTexture(texture.FileName, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                foreach (var texture in _textures) {
                    await renderer.SaveTextureAsync(Path.Combine(location, texture.FileName), texture.PreferredFormat, _color.ToColor(), _opacity);
                    if (cancellation.IsCancellationRequested) return;
                }
            }
        }

        public class TransparentIfFlagged : SolidColorIfFlagged {
            public TransparentIfFlagged([NotNull] TextureFileName[] textures, bool inverse) : base(textures, inverse, Colors.Black, 0d) { }

            public override string DisplayName { get; set; } = "Transparent If Enabled";
        }

        public class ReplacedIfFlagged : PaintableItem {
            public ReplacedIfFlagged(bool inverse, [NotNull] Dictionary<TextureFileName, PaintShopSource> replacements) : base(false) {
                _inverse = inverse;
                _replacements = replacements;
                AffectedTextures.AddRange(_replacements.Keys.Select(x => x.FileName));
            }

            public override string DisplayName { get; set; } = "Replaced If Enabled";

            private readonly bool _inverse;
            private readonly Dictionary<TextureFileName, PaintShopSource> _replacements;

            protected override bool IsActive() {
                return Enabled ^ _inverse;
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in _replacements) {
                    renderer.OverrideTexture(replacement.Key.FileName, replacement.Value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in _replacements) {
                    renderer.OverrideTexture(replacement.Key.FileName, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                foreach (var replacement in _replacements) {
                    if (replacement.Value.Data != null) {
                        await FileUtils.WriteAllBytesAsync(Path.Combine(location, replacement.Key.FileName), replacement.Value.Data);
                    } else if (replacement.Value.Name != null) {
                        await renderer.SaveTextureAsync(replacement.Key.FileName, replacement.Key.PreferredFormat, replacement.Value);
                    }

                    if (cancellation.IsCancellationRequested) return;
                }
            }
        }

        public class Replacement : PaintableItem {
            [NotNull]
            private readonly TextureFileName[] _textures;

            public Dictionary<string, PaintShopSource> Replacements { get; }

            public Replacement([NotNull] TextureFileName[] textures, [NotNull] Dictionary<string, PaintShopSource> replacements) : base(false) {
                _textures = textures;
                Replacements = replacements;
                Value = Replacements.FirstOrDefault();
                AffectedTextures.AddRange(_textures.Select(x => x.FileName));
            }

            private KeyValuePair<string, PaintShopSource> _value;

            public KeyValuePair<string, PaintShopSource> Value {
                get => _value;
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
                    renderer.OverrideTexture(tex.FileName, value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var tex in _textures) {
                    renderer.OverrideTexture(tex.FileName, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var tex in _textures) {
                    await renderer.SaveTextureAsync(Path.Combine(location, tex.FileName), tex.PreferredFormat, value);
                    if (cancellation.IsCancellationRequested) return;
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
            public Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> Replacements { get; }

            public MultiReplacement(Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> replacements) : base(false) {
                Replacements = replacements;
                Value = Replacements.FirstOrDefault();
                AffectedTextures.AddRange(Replacements.Values.SelectMany(x => x.Keys.Select(y => y.FileName)));
            }

            private KeyValuePair<string, Dictionary<TextureFileName, PaintShopSource>> _value;

            public KeyValuePair<string, Dictionary<TextureFileName, PaintShopSource>> Value {
                get => _value;
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
                    renderer.OverrideTexture(pair.Key.FileName, pair.Value);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var tex in AffectedTextures) {
                    renderer.OverrideTexture(tex, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                var value = Value.Value;
                if (value == null) return;
                foreach (var pair in value) {
                    await renderer.SaveTextureAsync(Path.Combine(location, pair.Key.FileName), pair.Key.PreferredFormat, pair.Value);
                    if (cancellation.IsCancellationRequested) return;
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
            protected readonly Dictionary<TextureFileName, TintedEntry> Replacements;

            public ColoredItem([Localizable(false)] TextureFileName diffuseTexture, Color defaultColor)
                    : this(diffuseTexture, new CarPaintColors(defaultColor)) {}

            public ColoredItem([Localizable(false)] TextureFileName diffuseTexture, CarPaintColors colors)
                    : this(new Dictionary<TextureFileName, TintedEntry> {
                        [diffuseTexture] = new TintedEntry(PaintShopSource.White, null, null)
                    }, colors) {}

            public ColoredItem(Dictionary<TextureFileName, TintedEntry> replacements, CarPaintColors colors) : base(false) {
                Replacements = replacements;
                Colors = colors;
                Colors.PropertyChanged += OnColorsChanged;
                AffectedTextures.AddRange(Replacements.Keys.Select(x => x.FileName));
            }

            // which color is in which slot, −1 if there is no color in given slot
            [CanBeNull]
            public int[] LiveryColorIds { get; set; }

            public override Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
                Slot = i,
                Color = x == -1 ? (Color?)null : ActualColors.ElementAtOrDefault(x)
            }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? base.LiveryColors;

            public override string DisplayName { get; set; } = "Colored item";

            public CarPaintColors Colors { get; }

            public Color[] ActualColors => Colors.ActualColors;

            private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
                OnPropertyChanged(nameof(ActualColors));
            }

            protected override void ApplyOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTextureTint(replacement.Key.FileName, Colors.DrawingColors, 0d,
                            replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                foreach (var replacement in Replacements) {
                    renderer.OverrideTexture(replacement.Key.FileName, null);
                }
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                foreach (var replacement in Replacements) {
                    await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key.FileName), replacement.Key.PreferredFormat,
                            Colors.DrawingColors, 0d, replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                    if (cancellation.IsCancellationRequested) return;
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

            public TintedWindows(Dictionary<TextureFileName, TintedEntry> replacements, CarPaintColors colors, double defaultAlpha = 0.23,
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

        public sealed class CarPaintColor : Displayable {
            public CarPaintColor(string name, Color defaultValue, [CanBeNull] Dictionary<string, Color> allowedValues) {
                AllowedValues = allowedValues?.Count == 0 ? null : allowedValues;
                DisplayName = name;
                Value = defaultValue;
            }

            private Color _value;

            public Color Value {
                get => _value;
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
                set => Value = value.Value;
            }

            [CanBeNull]
            public Dictionary<string, Color> AllowedValues { get; }
        }

        public class CarPaintColors : NotifyPropertyChanged {
            public CarPaintColors([NotNull] CarPaintColor[] colors) {
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

            [NotNull]
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
            public static CarPaintPattern Nothing => new CarPaintPattern("Nothing", PaintShopSource.Transparent, null, null, new CarPaintColors(), null);

            public CarPaintPattern(string name, [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource overlay, [CanBeNull] Size? size,
                    CarPaintColors colors, [CanBeNull] IEnumerable<PaintShopPatternNumbers> numbers) {
                DisplayName = name;
                Source = source;
                Overlay = overlay;
                Colors = colors;
                Size = size;
                Numbers = numbers?.ToList() ?? new List<PaintShopPatternNumbers>(0);
                colors.PropertyChanged += OnColorsChanged;
            }

            public string LiveryStyle { get; set; }

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

            [CanBeNull]
            public Size? Size { get; }

            [NotNull]
            public List<PaintShopPatternNumbers> Numbers { get; }

            public bool HasNumbers => Numbers.Count > 0;

            // which color is in which slot, −1 if there is no color in given slot
            [CanBeNull]
            public int[] LiveryColorIds { get; set; }

            [NotNull]
            public Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
                Slot = i,
                Color = x == -1 ? (Color?)null : ActualColors.ElementAtOrDefault(x)
            }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? ActualColors.ToDictionary((x, i) => i, (x, i) => x);
        }

        public class CarPaintReplacementSource {
            public CarPaintReplacementSource(PaintShopSource source, bool colored) {
                Source = source;
                Colored = colored;
            }

            public PaintShopSource Source { get; }
            public bool Colored { get; }
        }

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
                    result["colorReplacementValue"] = NameToId(ColorReplacementValue.Key, false);
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
                    var value = ColorReplacements?.FirstOrDefault(x => NameToId(x.Key, false) == loaded);
                    if (value?.Value != null) {
                        ColorReplacementValue = value.Value;
                    }
                }

                if (PatternTexture != null) {
                    PatternEnabled = data.GetBoolValueOnly("patternEnabled") != false;
                    var current = data.GetStringValueOnly("patternSelected");
                    CurrentPattern = Patterns.FirstOrDefault(x => string.Equals(x.DisplayName, current, StringComparison.OrdinalIgnoreCase)) ?? CurrentPattern;
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

        public class ComplexCarPaint : CarPaint {
            public TextureFileName MapsTexture { get; }

            [CanBeNull]
            public PaintShopSource MapsMask { get; }

            [NotNull]
            public PaintShopSource MapsDefaultTexture { get; }

            public bool FixGloss { get; internal set; }

            public ComplexCarPaint([Localizable(false)] TextureFileName mapsTexture, [NotNull] PaintShopSource mapsSource, [CanBeNull] PaintShopSource mapsMask) {
                MapsTexture = mapsTexture;
                MapsMask = mapsMask;
                MapsDefaultTexture = mapsSource;
                AffectedTextures.Add(mapsTexture.FileName);
            }

            private bool _complexMode;
            private bool? _previousComplexMode;

            [JsonProperty("complex")]
            public bool ComplexMode {
                get => _complexMode;
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
                get => _reflection;
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
                get => _gloss;
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
                get => _specular;
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
                        renderer.OverrideTextureMaps(MapsTexture.FileName, Reflection, Gloss, Specular, FixGloss,
                                MapsDefaultTexture, MapsMask);
                    } else {
                        renderer.OverrideTexture(MapsTexture.FileName, null);
                    }

                    _previousComplexMode = _complexMode;
                    _previousReflection = Reflection;
                    _previousGloss = Gloss;
                    _previousSpecular = Specular;
                }
            }

            protected override void ResetOverride(IPaintShopRenderer renderer) {
                base.ResetOverride(renderer);
                renderer.OverrideTexture(MapsTexture.FileName, null);
            }

            protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
                await base.SaveOverrideAsync(renderer, location, cancellation);
                if (cancellation.IsCancellationRequested) return;

                if (ComplexMode) {
                    await renderer.SaveTextureMapsAsync(Path.Combine(location, MapsTexture.FileName), MapsTexture.PreferredFormat, Reflection, Gloss, Specular,
                            FixGloss, MapsDefaultTexture, MapsMask);
                }
            }
        }

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
                    CurrentPattern = Patterns.FirstOrDefault(x => string.Equals(x.DisplayName, current, StringComparison.OrdinalIgnoreCase)) ?? CurrentPattern;
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
}