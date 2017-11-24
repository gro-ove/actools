using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.PaintShop;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Kn5File;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Image = System.Drawing.Image;
using Size = System.Windows.Size;

namespace AcManager.Tools {
    public class LiveryResource : SettingEntry {
        [NotNull]
        private readonly string _filename;

        private LiveryResource(string id, string displayName, string filename) : base(id, displayName) {
            _filename = filename;
        }

        public FrameworkElement Load() {
            using (var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return (FrameworkElement)XamlReader.Load(fs, new ParserContext {
                    BaseUri = new Uri(_filename, UriKind.Absolute)
                });
            }
        }

        private static LiveryResource FromFilename([NotNull] string filename) {
            var id = Path.GetFileNameWithoutExtension(filename);
            return new LiveryResource(id, AcStringValues.NameFromId(id), filename);
        }

        public static LiveryResource[] FromFilesStorage([NotNull] string category, bool prependNone = false) {
            var enumerable = FilesStorage.Instance.GetContentFilesFiltered("*.xaml", ContentCategory.Livery, category)
                        .Select(x => FromFilename(x.Filename));
            if (prependNone) {
                enumerable = enumerable.Prepend(new LiveryResource("", ToolsStrings.Common_None, null));
            }
            return enumerable.ToArray();
        }
    }

    public class LiveryIconEditor : NotifyPropertyChanged {
        private const string KeyStyle = "__LiveryIconEditor.style";
        private const string KeyShape = "__LiveryIconEditor.shape";
        private const string KeyNumbers = "__LiveryIconEditor.numbers";

        public LiveryResource[] Numbers { get; } = LiveryResource.FromFilesStorage("Numbers", true);
        public LiveryResource[] Shapes { get; } = LiveryResource.FromFilesStorage("Shapes");
        public LiveryResource[] Styles { get; } = LiveryResource.FromFilesStorage("Styles");

        public bool HasSecondaryColor => (CustomShape ? StyleColorsNumber : ShapeColorsNumber) > 1;
        public bool HasTertiaryColor => (CustomShape ? StyleColorsNumber : ShapeColorsNumber) > 2;

        private int _shapeColorsNumber;

        public int ShapeColorsNumber {
            get => _shapeColorsNumber;
            set {
                value = value.Clamp(1, 3);
                if (Equals(value, _shapeColorsNumber)) return;
                _shapeColorsNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecondaryColor));
                OnPropertyChanged(nameof(HasTertiaryColor));
            }
        }

        private int _styleColorsNumber;

        public int StyleColorsNumber {
            get => _styleColorsNumber;
            set {
                if (Equals(value, _styleColorsNumber)) return;
                _styleColorsNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecondaryColor));
                OnPropertyChanged(nameof(HasTertiaryColor));
            }
        }

        private bool _customShape;

        public bool CustomShape {
            get => _customShape;
            set {
                if (Equals(value, _customShape)) return;
                _customShape = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSecondaryColor));
                OnPropertyChanged(nameof(HasTertiaryColor));
            }
        }

        private void LoadProperties(FrameworkElement element, bool styleMode) {
            if (styleMode) {
                CustomShape = false;
                StyleColorsNumber = 3;
            } else {
                ShapeColorsNumber = 3;
            }

            if (!(element.Tag is string properties)) return;

            foreach (var s in properties.Split(';')) {
                var pair = s.Split(new[] { '=', ':' }, 2);
                if (pair.Length != 2) continue;

                var key = pair[0].ToLowerInvariant();
                if (styleMode) {
                    switch (key) {
                        case "customshape":
                            CustomShape = string.Equals(pair[1], @"true", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }

                switch (key) {
                    case "colors":
                        if (styleMode) {
                            StyleColorsNumber = FlexibleParser.ParseInt(pair[1], 3);
                        } else {
                            ShapeColorsNumber = FlexibleParser.ParseInt(pair[1], 3);
                        }
                        break;
                }
            }
        }

        private LiveryResource _selectedShape;

        public LiveryResource SelectedShape {
            get => _selectedShape;
            set {
                if (Equals(value, _selectedShape)) return;
                _selectedShape = value;
                OnPropertyChanged();

                try {
                    var obj = value.Load();
                    LoadProperties(obj, false);
                    obj.DataContext = this;
                    ShapeObject = obj;
                    if (!_quickMode) ValuesStorage.Set(KeyShape, value.Id);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        private LiveryResource _selectedNumbers;

        public LiveryResource SelectedNumbers {
            get => _selectedNumbers;
            set {
                if (Equals(value, _selectedNumbers)) return;
                _selectedNumbers = value;
                OnPropertyChanged();

                try {
                    if (value.Id != string.Empty) {
                        var obj = value.Load();
                        obj.DataContext = this;
                        NumbersObject = obj;
                        if (!_quickMode) ValuesStorage.Set(KeyNumbers, value.Id);
                    } else {
                        NumbersObject = null;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        private LiveryResource _selectedStyle;
        private FrameworkElement _styleObject;

        public LiveryResource SelectedStyle {
            get => _selectedStyle;
            set {
                if (Equals(value, _selectedStyle)) return;
                _selectedStyle = value;
                OnPropertyChanged();

                try {
                    _styleObject = value.Load();
                    LoadProperties(_styleObject, true);
                    _styleObject.DataContext = this;

                    if (_preview != null) {
                        _preview.Content = _styleObject;
                    }

                    if (!_quickMode) ValuesStorage.Set(KeyStyle, value.Id);
                } catch (Exception e) {
                    Logging.Warning(e);

                    if (_preview != null) {
                        _preview.Content = null;
                    }
                }
            }
        }

        #region Constructor
        [CanBeNull]
        public CarSkinObject Skin { get; }

        [CanBeNull]
        private readonly ContentPresenter _preview;
        private readonly bool _quickMode;

        public LiveryIconEditor(CarSkinObject skin, bool quickMode, bool randomMode, [CanBeNull] string preferredStyle, [CanBeNull] ContentPresenter preview) {
            BaseObject = new Grid { Width = CommonAcConsts.LiveryWidth, Height = CommonAcConsts.LiveryHeight };
            Base = new VisualBrush(BaseObject);

            _preview = preview;
            _quickMode = quickMode;
            Skin = skin;

            if (preferredStyle != null) {
                SelectedShape = Shapes.GetByIdOrDefault(preferredStyle) ?? Shapes.FirstOrDefault(x => x.DisplayName == preferredStyle) ??
                        Shapes.RandomElement();
            } else if (randomMode) {
                SelectedShape = Shapes.RandomElement();
            } else {
                SelectedShape = Shapes.GetByIdOrDefault(ValuesStorage.GetString(KeyShape)) ?? Shapes.FirstOrDefault();
            }

            SelectedNumbers = string.IsNullOrWhiteSpace(skin.SkinNumber) || skin.SkinNumber == @"0"
                    ? Numbers.FirstOrDefault() : Numbers.GetByIdOrDefault(ValuesStorage.GetString(KeyNumbers)) ?? Numbers.FirstOrDefault();
            SelectedStyle = Styles.GetByIdOrDefault(ValuesStorage.GetString(KeyStyle)) ?? Styles.FirstOrDefault();

            Value = string.IsNullOrWhiteSpace(skin.SkinNumber) ? @"0" : skin.SkinNumber;
            TextColorValue = Colors.White;
        }

        public async Task ApplyColorsAsync([CanBeNull] Color[] colors) {
            colors = colors ?? await GuessColorsAsync(Skin);
            ColorValue = colors.Length > 0 ? colors[0] : Colors.White;
            SecondaryColorValue = colors.Length > 1 ? colors[1] : Colors.Black;
            TertiaryColorValue = colors.Length > 2 ? colors[2] : Colors.Black;
        }

        public Task GuessColorsAsync() {
            return ApplyColorsAsync(null);
        }
        #endregion

        #region Might be referenced by styles/shapes/numbers
        public Grid BaseObject { get; }
        public VisualBrush Base { get; }
        public Brush Shape { get; private set; }
        public Brush Number { get; private set; }
        #endregion

        #region View model stuff
        private FrameworkElement _shapeObject;

        [NotNull]
        public FrameworkElement ShapeObject {
            get => _shapeObject;
            set {
                if (Equals(value, _shapeObject)) return;
                _shapeObject = value;
                _shapeObject.Width = CommonAcConsts.LiveryWidth;
                _shapeObject.Height = CommonAcConsts.LiveryHeight;
                OnPropertyChanged();

                BaseObject.Children.Clear();
                BaseObject.Children.Add(_shapeObject);
                if (_numbersObject != null) {
                    BaseObject.Children.Add(_numbersObject);
                }

                Shape = new VisualBrush(value) { Stretch = Stretch.None };
                OnPropertyChanged(nameof(Shape));
            }
        }

        private FrameworkElement _numbersObject;

        [CanBeNull]
        public FrameworkElement NumbersObject {
            get => _numbersObject;
            set {
                if (Equals(value, _numbersObject)) return;
                _numbersObject = value;
                OnPropertyChanged();

                BaseObject.Children.Clear();
                BaseObject.Children.Add(_shapeObject);

                if (_numbersObject != null) {
                    _numbersObject.Width = CommonAcConsts.LiveryWidth;
                    _numbersObject.Height = CommonAcConsts.LiveryHeight;
                    BaseObject.Children.Add(_numbersObject);

                    Number = new VisualBrush(value) { Stretch = Stretch.None };
                } else {
                    Number = new SolidColorBrush(Colors.Transparent);
                }

                OnPropertyChanged(nameof(Number));
            }
        }

        private Color? _colorValue;

        public Color ColorValue {
            get => _colorValue ?? Colors.Black;
            set {
                if (Equals(value, _colorValue)) return;
                _colorValue = value;
                OnPropertyChanged();

                Color = new SolidColorBrush(value);
                OnPropertyChanged(nameof(Color));
            }
        }

        public SolidColorBrush Color { get; private set; }

        private Color? _secondaryColorValue;

        public Color SecondaryColorValue {
            get => _secondaryColorValue ?? Colors.Black;
            set {
                if (Equals(value, _secondaryColorValue)) return;
                _secondaryColorValue = value;
                OnPropertyChanged();

                SecondaryColor = new SolidColorBrush(value);
                OnPropertyChanged(nameof(SecondaryColor));
            }
        }

        public SolidColorBrush SecondaryColor { get; private set; }

        private Color? _tertiaryColorValue;

        public Color TertiaryColorValue {
            get => _tertiaryColorValue ?? Colors.Black;
            set {
                if (Equals(value, _tertiaryColorValue)) return;
                _tertiaryColorValue = value;
                OnPropertyChanged();

                TertiaryColor = new SolidColorBrush(value);
                OnPropertyChanged(nameof(TertiaryColor));
            }
        }

        public SolidColorBrush TertiaryColor { get; private set; }

        private string _value;

        public string Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NumberValue));
            }
        }

        public int NumberValue {
            get => _value.AsInt();
            set => Value = value.ToInvariantString();
        }

        private Color? _textColorValue;

        public Color TextColorValue {
            get => _textColorValue ?? Colors.Black;
            set {
                if (Equals(value, _textColorValue)) return;
                _textColorValue = value;
                OnPropertyChanged();

                TextColor = new SolidColorBrush(value);
                OnPropertyChanged(nameof(TextColor));

                BaseObject.SetValue(TextBlock.ForegroundProperty, TextColor);
            }
        }

        public SolidColorBrush TextColor { get; private set; }
        #endregion

        #region Generation
        public void CreateNewIcon() {
            var size = new Size(CommonAcConsts.LiveryWidth, CommonAcConsts.LiveryHeight);

            var result = new ContentPresenter {
                Width = CommonAcConsts.LiveryWidth,
                Height = CommonAcConsts.LiveryHeight,
                Content = _styleObject
            };

            result.Measure(size);
            result.Arrange(new Rect(size));
            result.ApplyTemplate();
            result.UpdateLayout();

            if (Skin == null) return;
            var bmp = new RenderTargetBitmap(CommonAcConsts.LiveryWidth, CommonAcConsts.LiveryHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(result);
            bmp.SaveAsPng(Skin.LiveryImage);
        }
        #endregion

        #region Static methods

        public static async Task GenerateAsync(CarSkinObject target, Color[] colors, string preferredStyle) {
            var editor = new LiveryIconEditor(target, true, false, preferredStyle, null);
            await editor.ApplyColorsAsync(colors);
            editor.CreateNewIcon();
        }

        public static async Task GenerateAsync(CarSkinObject target) {
            var editor = new LiveryIconEditor(target, true, false, null, null);
            await editor.GuessColorsAsync();
            editor.CreateNewIcon();
        }

        public static async Task GenerateRandomAsync(CarSkinObject target) {
            var editor = new LiveryIconEditor(target, true, true, null, null);
            await editor.GuessColorsAsync();
            editor.CreateNewIcon();
        }
        #endregion

        #region Guessing colors
        private static readonly WeakList<Tuple<string, Kn5>> Kn5MaterialsCache = new WeakList<Tuple<string, Kn5>>(10);
        private static readonly WeakList<Tuple<string, Kn5>> Kn5TexturesCache = new WeakList<Tuple<string, Kn5>>(10);

        [ItemCanBeNull]
        private static async Task<Color[]> GuessColorsFromTexturesAsync(CarSkinObject skin) {
            if (!ImageUtils.IsMagickSupported) {
                Logging.Debug("ImageMagick is missing");
                return null;
            }

            var car = CarsManager.Instance.GetById(skin.CarId);
            if (car == null) {
                Logging.Debug("Car not found");
                return null;
            }

            Kn5MaterialsCache.Purge();
            Kn5TexturesCache.Purge();

            string kn5Filename = null;

            string GetKn5Filename() {
                return kn5Filename ?? (kn5Filename = AcPaths.GetMainCarFilename(car.Location, car.AcdData));
            }

            var materialsKn5 = Kn5MaterialsCache.FirstOrDefault(x => x?.Item1 == car.Id)?.Item2;
            if (materialsKn5 == null) {
                if (!File.Exists(GetKn5Filename())) {
                    Logging.Debug("KN5 not found");
                    return null;
                }

                materialsKn5 = await Task.Run(() => Kn5.FromFile(GetKn5Filename(), SkippingTextureLoader.Instance, nodeLoader: SkippingNodeLoader.Instance));
                Kn5MaterialsCache.Add(Tuple.Create(car.Id, materialsKn5));
            }

            var paintable = (await PaintShop.PaintShop.GetPaintableItemsAsync(skin.CarId, materialsKn5, default(CancellationToken)))?.ToList();
            if (paintable == null) {
                return null;
            }

            var carPaint = paintable.OfType<CarPaint>().FirstOrDefault();
            if (carPaint?.GuessColorsFromPreviews != false) {
                return null;
            }

            var texture = carPaint.DetailsTexture?.TextureName;
            if (texture == null) {
                Logging.Debug("Details texture not found");
                return null;
            }

            if (!File.Exists(Path.Combine(skin.Location, texture))) {
                Logging.Debug("Details texture not overridden, could be a fully custom skin");
                return null;
            }

            TextureReader reader = null;
            TextureReader GetReader() => reader ?? (reader = new TextureReader());

            var s = Stopwatch.StartNew();
            try {
                Kn5 texturesKn5 = null;

                Color GetColor(string textureName) {
                    if (textureName == null) return Colors.White;

                    var filename = Path.Combine(skin.Location, textureName);
                    if (!File.Exists(filename)) {
                        if (texturesKn5 == null) {
                            texturesKn5 = Kn5TexturesCache.FirstOrDefault(x => x?.Item1 == car.Id)?.Item2;
                            if (texturesKn5 == null) {
                                if (!File.Exists(GetKn5Filename())) return Colors.White;
                                texturesKn5 = Kn5.FromFile(kn5Filename, materialLoader: SkippingMaterialLoader.Instance, nodeLoader: SkippingNodeLoader.Instance);
                                Kn5TexturesCache.Add(Tuple.Create(car.Id, texturesKn5));
                            }
                        }

                        var bytes = texturesKn5?.TexturesData.GetValueOrDefault(textureName);
                        return bytes == null ? Colors.White :
                                ImageUtils.GetTextureColor(GetReader().ToPngNoFormat(bytes, true, new System.Drawing.Size(16, 16))).ToColor();
                    }

                    var asPng = GetReader().ToPngNoFormat(File.ReadAllBytes(filename), true, new System.Drawing.Size(16, 16));
                    File.WriteAllBytes(filename + ".png", asPng);
                    return ImageUtils.GetTextureColor(asPng).ToColor();
                }

                return await Task.Run(() => {
                    var result = new[] {
                        GetColor(texture),
                        Colors.Black,
                        Colors.Black
                    };

                    Logging.Debug($"Main color: {texture} ({result[0].ToHexString()})");

                    foreach (var item in paintable.OfType<ColoredItem>().Where(x => x.LiveryColorIds?.Length > 0).OrderBy(x => x.LiveryPriority)) {
                        if (item.LiveryColorIds == null) continue;
                        for (var i = 0; i < item.LiveryColorIds.Length; i++) {
                            var slotId = i;
                            var slotTexture = item.GetAffectedTextures().ElementAtOrDefault(item.LiveryColorIds[i]);
                            Logging.Debug($"Extra: {slotId} = {slotTexture} (priority: {item.LiveryPriority})");

                            if (slotId < 0 || slotId > 2 || slotTexture == null) continue;
                            result[slotId] = GetColor(slotTexture);
                        }
                    }

                    Logging.Debug($"Colors guessed: {s.Elapsed.TotalMilliseconds:F1} ms");
                    return result;
                });
            } finally {
                DisposeHelper.Dispose(ref reader);
            }
        }

        [NotNull]
        private static async Task<Color[]> GuessColorsAsync(CarSkinObject skin) {
            try {
                var result = await GuessColorsFromTexturesAsync(skin);
                if (result != null) {
                    return result;
                }
            } catch (Exception e) {
                Logging.Warning("Can’t guess colors with Paint Shop: " + e);
            }

            return await Task.Run(() => {
                try {
                    using (var bitmap = Image.FromFile(skin.PreviewImage)) {
                        var baseColors = ImageUtils.GetBaseColors((Bitmap)bitmap);
                        Logging.Debug("Colors from preview: " + baseColors.Select(x => x.ToString()).JoinToString(", "));

                        var a = baseColors.Select(x => (System.Drawing.Color?)x).FirstOrDefault()?.ToColor() ?? Colors.White;
                        var b = baseColors.Select(x => (System.Drawing.Color?)x).ElementAtOrDefault(1)?.ToColor() ?? Colors.Black;
                        var c = baseColors.Select(x => (System.Drawing.Color?)x).ElementAtOrDefault(2)?.ToColor() ?? Colors.Black;
                        return new[] { a, b, c };
                    }
                } catch (Exception e) {
                    Logging.Warning("Can’t guess colors: " + e);
                    return new[] { Colors.White, Colors.Black, Colors.Black };
                }
            });
        }
        #endregion
    }
}