using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Image = System.Drawing.Image;
using Size = System.Windows.Size;

namespace AcManager.Pages.Dialogs {
    public partial class LiveryIconEditor : INotifyPropertyChanged {
        public const string KeyStyle = "__LiveryIconEditor.style";
        public const string KeyShape = "__LiveryIconEditor.shape";
        public const string KeyNumbers = "__LiveryIconEditor.numbers";

        public SettingEntry[] Numbers { get; } = {
            new SettingEntry("", "None"),
            new SettingEntry("Condensed", "Condensed"),
            new SettingEntry("Light", "Light"),
            new SettingEntry("LightOutlined", "Light (with contour)"),
        };

        public SettingEntry[] Shapes { get; } = {
            new SettingEntry("Flat", "Flat"),
            new SettingEntry("Diagonal", "Diagonal"),
            new SettingEntry("Stripes", "Stripes"),
            new SettingEntry("StripesSide", "Side Stripes"),
            new SettingEntry("DoubleStripes", "Double Stripes"),
            new SettingEntry("HorizontalStripes", "Horizontal Stripes"),
            new SettingEntry("Circle", "Circle"),
            new SettingEntry("DiagonalWithCircle", "Diagonal with Circle"),
            new SettingEntry("DiagonalLineWithCircle", "Diagonal line with Circle"),
            new SettingEntry("Carbon", "Carbon")
        };

        public SettingEntry[] Styles { get; } = {
            new SettingEntry("Solid", "Solid"),
            new SettingEntry("Gradient", "Gradient"),
            new SettingEntry("Gloss", "Gloss"),
            new SettingEntry("Metallic", "Metallic"),
            new SettingEntry("Miura", "Miura"),
        };

        public CarSkinObject Skin { get; set; }

        public bool HasSecondaryColor => (CustomShape ? StyleColorsNumber : ShapeColorsNumber) > 1;

        public bool HasTertiaryColor => (CustomShape ? StyleColorsNumber : ShapeColorsNumber) > 2;

        private int _shapeColorsNumber;

        public int ShapeColorsNumber {
            get { return _shapeColorsNumber; }
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
            get { return _styleColorsNumber; }
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
            get { return _customShape; }
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

            var properties = element.Tag as string;
            if (properties == null) return;

            foreach (var s in properties.Split(';')) {
                var pair = s.Split(new [] { '=', ':' }, 2);
                if (pair.Length != 2) continue;

                var key = pair[0].ToLowerInvariant();
                if (styleMode) {
                    switch (key) {
                        case "customshape":
                            CustomShape = string.Equals(pair[1], "true", StringComparison.OrdinalIgnoreCase);
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

        private SettingEntry _selectedShape;

        public SettingEntry SelectedShape {
            get { return _selectedShape; }
            set {
                if (Equals(value, _selectedShape)) return;
                _selectedShape = value;
                OnPropertyChanged();

                try {
                    var obj = (FrameworkElement)Application.LoadComponent(new Uri($"/Assets/Livery/Shapes/{value.Id}.xaml", UriKind.Relative));
                    LoadProperties(obj, false);
                    obj.DataContext = Model;
                    Model.ShapeObject = obj;
                    if (!_quickMode) ValuesStorage.Set(KeyShape, value.Id);
                } catch (Exception e) {
                    Logging.Warning("[LiveryIconEditor] Can’t change shape: " + e);
                }
            }
        }

        private SettingEntry _selectedNumbers;

        public SettingEntry SelectedNumbers {
            get { return _selectedNumbers; }
            set {
                if (Equals(value, _selectedNumbers)) return;
                _selectedNumbers = value;
                OnPropertyChanged();

                try {
                    if (value.Id != string.Empty) {
                        var obj = (FrameworkElement)Application.LoadComponent(new Uri($"/Assets/Livery/Numbers/{value.Id}.xaml", UriKind.Relative));
                        obj.DataContext = Model;
                        Model.NumbersObject = obj;
                        if (!_quickMode) ValuesStorage.Set(KeyNumbers, value.Id);
                    } else {
                        Model.NumbersObject = null;
                    }
                } catch (Exception e) {
                    Logging.Warning("[LiveryIconEditor] Can’t change numbers: " + e);
                }
            }
        }

        private SettingEntry _selectedStyle;

        public SettingEntry SelectedStyle {
            get { return _selectedStyle; }
            set {
                if (Equals(value, _selectedStyle)) return;
                _selectedStyle = value;
                OnPropertyChanged();

                try {
                    StyleObject = (FrameworkElement)Application.LoadComponent(new Uri($"/Assets/Livery/Styles/{value.Id}.xaml", UriKind.Relative));
                    LoadProperties(StyleObject, true);
                    StyleObject.DataContext = Model;
                    OnPropertyChanged(nameof(StyleObject));
                    if (!_quickMode) ValuesStorage.Set(KeyStyle, value.Id);
                } catch (Exception e) {
                    Logging.Warning("[LiveryIconEditor] Can’t change style: " + e);
                }
            }
        }

        public FrameworkElement StyleObject { get; private set; }

        public LiveryIconEditor(CarSkinObject skin) : this (skin, false, false){ }

        private readonly bool _quickMode;

        private LiveryIconEditor(CarSkinObject skin, bool quickMode, bool randomMode) {
            _quickMode = quickMode;
            Skin = skin;

            DataContext = this;
            InitializeComponent();

            SelectedStyle = Styles.GetByIdOrDefault(ValuesStorage.GetString(KeyStyle)) ?? Styles.FirstOrDefault();
            SelectedNumbers = string.IsNullOrWhiteSpace(skin.SkinNumber) || skin.SkinNumber == "0"
                    ? Numbers.FirstOrDefault() : Numbers.GetByIdOrDefault(ValuesStorage.GetString(KeyNumbers)) ?? Numbers.FirstOrDefault();
            if (randomMode) {
                SelectedShape = Shapes.RandomElement();
            } else {
                SelectedShape = Shapes.GetByIdOrDefault(ValuesStorage.GetString(KeyShape)) ?? Shapes.FirstOrDefault();
            }

            Buttons = new[] { OkButton, CancelButton };
            Model.Value = string.IsNullOrWhiteSpace(skin.SkinNumber) ? "0" : skin.SkinNumber;
            Model.TextColorValue = Colors.White;

            try {
                using (var bitmap = Image.FromFile(skin.PreviewImage)) {
                    var colors = ImageUtils.GetBaseColors((Bitmap)bitmap);
                    Model.ColorValue = colors.Select(x => (System.Drawing.Color?)x).FirstOrDefault()?.ToColor() ?? Colors.White;
                    Model.SecondaryColorValue = colors.Select(x => (System.Drawing.Color?)x).ElementAtOrDefault(1)?.ToColor() ?? Colors.Black;
                    Model.TertiaryColorValue = colors.Select(x => (System.Drawing.Color?)x).ElementAtOrDefault(2)?.ToColor() ?? Colors.Black;
                }
            } catch (Exception e) {
                Logging.Warning("[LiveryIconEditor] Can’t find base colors: " + e);
                Model.ColorValue = Colors.White;
                Model.SecondaryColorValue = Colors.Black;
                Model.TertiaryColorValue = Colors.Black;
            }
        }

        public new bool ShowDialog() {
            base.ShowDialog();
            return IsResultOk;
        }

        public StyleViewModel Model { get; } = new StyleViewModel();

        public class StyleViewModel : NotifyPropertyChanged {
            public Grid BaseObject { get; }

            public VisualBrush Base { get; }

            internal StyleViewModel() {
                BaseObject = new Grid {
                    Width = 64,
                    Height = 64
                };
                Base = new VisualBrush(BaseObject);
            }

            private FrameworkElement _shapeObject;

            [NotNull]
            public FrameworkElement ShapeObject {
                get { return _shapeObject; }
                set {
                    if (Equals(value, _shapeObject)) return;
                    _shapeObject = value;
                    _shapeObject.Width = 64;
                    _shapeObject.Height = 64;
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
                get { return _numbersObject; }
                set {
                    if (Equals(value, _numbersObject)) return;
                    _numbersObject = value;
                    OnPropertyChanged();

                    BaseObject.Children.Clear();
                    BaseObject.Children.Add(_shapeObject);

                    if (_numbersObject != null) {
                        _numbersObject.Width = 64;
                        _numbersObject.Height = 64;
                        BaseObject.Children.Add(_numbersObject);

                        Number = new VisualBrush(value) { Stretch = Stretch.None };
                    } else {
                        Number = new SolidColorBrush(Colors.Transparent);
                    }

                    OnPropertyChanged(nameof(Number));
                }
            }

            public Brush Shape { get; private set; }

            public Brush Number { get; private set; }

            private Color? _colorValue;

            public Color ColorValue {
                get { return _colorValue ?? Colors.Black; }
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
                get { return _secondaryColorValue ?? Colors.Black; }
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
                get { return _tertiaryColorValue ?? Colors.Black; }
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
                get { return _value; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(NumberValue));
                }
            }

            public int NumberValue {
                get { return _value.AsInt(); }
                set { Value = value.ToInvariantString(); }
            }

            private Color? _textColorValue;

            public Color TextColorValue {
                get { return _textColorValue ?? Colors.Black; }
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
        }

        private static readonly Action EmptyDelegate = delegate { };

        private void OnClosing(object sender, CancelEventArgs e) {
            if (IsResultOk) {
                CreateNewIcon().Forget();
            }
        }

        private async Task CreateNewIcon() {
            var size = new Size(64, 64);
            Result.Measure(size);
            Result.Arrange(new Rect(size));
            Result.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);

            await Task.Delay(100);
            if (Skin == null) return;
            
            // TODO: Save style?

            var bmp = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(Result);

            if (Model.Value != "0" && Model.Value != Skin.SkinNumber &&
                    ShowMessage($"Change skin’s number to “{Model.Value}”?", "Skin number is changed", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                Skin.SkinNumber = Model.Value;
            }

            try {
                bmp.SaveAsPng(Skin.LiveryImage);
            } catch (Exception e) {
                NonfatalError.Notify(@"Can’t change livery image", "Make sure the original file isn’t busy", e);
            }
        }

        public static async Task GenerateAsync(CarSkinObject target) {
            await new LiveryIconEditor(target, true, false).CreateNewIcon();
        }

        public static async Task GenerateRandomAsync(CarSkinObject target) {
            await new LiveryIconEditor(target, true, true).CreateNewIcon();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
