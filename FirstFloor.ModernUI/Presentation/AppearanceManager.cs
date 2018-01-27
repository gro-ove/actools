using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public partial class AppearanceManager : NotifyPropertyChanged {
        public static AppearanceManager Current { get; } = new AppearanceManager();

        public static Uri BaseValuesSource = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.xaml", UriKind.Relative);
        public static Uri DefaultValuesSource = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Default.xaml", UriKind.Relative);
        public static readonly Uri DarkThemeSource = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Dark.xaml", UriKind.Relative);

        public const string KeyFormattingMode = "FormattingMode";

        private const string KeyAccentColor = "AccentColor";
        private const string KeyAccent = "Accent";
        private const string KeyDefaultFontSize = "DefaultFontSize";
        private const string KeyFixedFontSize = "FixedFontSize";
        private const string KeySubMenuFontSize = "ModernSubMenuFontSize";
        private const string KeySubMenuDraggablePoints = "ModernSubMenuDraggablePoints";

        private const string KeyTitleLinksTemplate = "TitleLinksTemplate";
        private const string KeyTitleLinksDefaultTemplate = "DefaultTitleLinksTemplate";
        private const string KeyTitleLinksLargerTemplate = "LargerTitleLinksTemplate";
        private const string KeyTitleLinksWeightTemplate = "TitleLinksWeight";

        public event EventHandler ThemeChange;
        public event EventHandler ThemeObsolete;

        private AppearanceManager() {}

        public void Initialize() {
            /*Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                [@"DefaultFont"] = new FontFamily(@"Courier New"),
                [@"CondensedFont"] = new FontFamily(@"Impact"),
            });

            TextElement.FontFamilyProperty.OverrideMetadata(
                    typeof(TextElement),
                    new FrameworkPropertyMetadata(
                            new FontFamily("Comic Sans MS")));

            TextBlock.FontFamilyProperty.OverrideMetadata(
                    typeof(TextBlock),
                    new FrameworkPropertyMetadata(
                            new FontFamily("Comic Sans MS")));*/

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = BaseValuesSource });
        }

        private ResourceDictionary _currentThemeDictionary;

        [CanBeNull]
        public ResourceDictionary CurrentThemeDictionary {
            get => _currentThemeDictionary;
            private set {
                if (Equals(value, _currentThemeDictionary)) return;
                _currentThemeDictionary = value;
                OnPropertyChanged();
            }
        }

        public void SetTheme([CanBeNull] ResourceDictionary dictionary) {
            SharedResourceDictionary.ClearCache();

            if (dictionary != null) {
                var defaultDictionary = new ResourceDictionary { Source = DefaultValuesSource };
                foreach (var key in defaultDictionary.Keys) {
                    if (!dictionary.Contains(key)) {
                        dictionary[key] = defaultDictionary[key];
                    }
                }
            }

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (dictionary != null) {
                dictionaries.Add(dictionary);
            }

            if (CurrentThemeDictionary != null) {
                dictionaries.Remove(CurrentThemeDictionary);
            }

            CurrentThemeDictionary = dictionary;
            ThemeChange?.Invoke(this, EventArgs.Empty);
        }

        public void SetTheme(Uri source) {
            SetTheme(new ResourceDictionary { Source = source });
        }

        private bool? _idealFormattingMode;
        private bool _idealFormattingModeSet;

        public bool? IdealFormattingMode {
            get => _idealFormattingMode;
            set {
                if (_idealFormattingModeSet && Equals(_idealFormattingMode, value)) return;
                _idealFormattingModeSet = true;
                _idealFormattingMode = value;

                if (value.HasValue) {
                    Application.Current.Resources[KeyFormattingMode] = value.Value ? TextFormattingMode.Ideal : TextFormattingMode.Display;
                } else {
                    Application.Current.Resources.Remove(KeyFormattingMode);
                }

                foreach (var window in Application.Current.Windows.OfType<DpiAwareWindow>()) {
                    window.UpdateTextFormatting();
                }

                OnPropertyChanged(nameof(IdealFormattingMode));
            }
        }

        public FontSize FontSize {
            get => Equals(Application.Current.Resources[KeyDefaultFontSize] as double? ?? 0d, 12D) ? FontSize.Small : FontSize.Large;
            set {
                if (FontSize == value) return;
                Application.Current.Resources[KeyDefaultFontSize] = value == FontSize.Small ? 12D : 13D;
                Application.Current.Resources[KeyFixedFontSize] = value == FontSize.Small ? 10.667D : 13.333D;
                OnPropertyChanged(nameof(FontSize));
            }
        }

        private bool? _largerTitleLinks;
        public bool LargerTitleLinks {
            get => _largerTitleLinks ?? false;
            set {
                if (_largerTitleLinks == value) return;
                _largerTitleLinks = value;
                Application.Current.Resources[KeyTitleLinksTemplate] = value ?
                        Application.Current.TryFindResource(KeyTitleLinksLargerTemplate) :
                        Application.Current.TryFindResource(KeyTitleLinksDefaultTemplate);
                OnPropertyChanged(nameof(LargerTitleLinks));
            }
        }

        private bool? _boldTitleLinks;
        public bool BoldTitleLinks {
            get => _boldTitleLinks ?? false;
            set {
                if (_boldTitleLinks == value) return;
                _boldTitleLinks = value;
                Application.Current.Resources[KeyTitleLinksWeightTemplate] = value ? FontWeights.Bold : FontWeights.Normal;
                OnPropertyChanged(nameof(BoldTitleLinks));
            }
        }

        public FontSize SubMenuFontSize {
            get => Equals(Application.Current.Resources[KeySubMenuFontSize] as double? ?? 0d, 11D) ? FontSize.Small : FontSize.Large;
            set {
                if (SubMenuFontSize == value) return;
                Application.Current.Resources[KeySubMenuFontSize] = value == FontSize.Small ? 11D : 14D;
                OnPropertyChanged();
            }
        }

        public bool SubMenuDraggablePoints {
            get => Equals(Application.Current.Resources[KeySubMenuDraggablePoints] as Visibility?, Visibility.Visible);
            set {
                Application.Current.Resources[KeySubMenuDraggablePoints] = value ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyChanged(nameof(SubMenuDraggablePoints));
            }
        }

        public Color AccentColor {
            get => Application.Current.Resources[KeyAccentColor] as Color? ?? Color.FromArgb(0xff, 0x1b, 0xa1, 0xe2);
            set {
                Application.Current.Resources[KeyAccentColor] = value;
                Application.Current.Resources[KeyAccent] = new SolidColorBrush(value);

                if (CurrentThemeDictionary != null) {
                    if (CurrentThemeDictionary.Source != null) {
                        SetTheme(CurrentThemeDictionary.Source);
                    } else {
                        ThemeObsolete?.Invoke(this, EventArgs.Empty);
                    }
                }

                OnPropertyChanged();
            }
        }

        private DateTime _lastSet;
        private bool _settingInProgress;
        private Color? _actualColor;

        public async void SetAccentColorAsync(Color value) {
            if (_settingInProgress) {
                _actualColor = value;
                return;
            }

            if (DateTime.Now - _lastSet > TimeSpan.FromSeconds(1)) {
                AccentColor = value;
                _lastSet = DateTime.Now;
            } else {
                _settingInProgress = true;

                try {
                    do {
                        if (_actualColor.HasValue) {
                            value = _actualColor.Value;
                            _actualColor = null;
                        }
                        await Task.Delay(300).ConfigureAwait(false);
                    } while (_actualColor.HasValue);
                    ActionExtension.InvokeInMainThread(() => AccentColor = value);
                    _lastSet = DateTime.Now;
                } finally {
                    _settingInProgress = false;
                }
            }
        }

        private readonly StoredValue<bool> _forceMenuAtTopInFullscreenMode = Stored.Get("/appearance_forceMenuAtTopInFullscreenMode", true);

        public bool ForceMenuAtTopInFullscreenMode {
            get => _forceMenuAtTopInFullscreenMode.Value;
            set {
                if (Equals(value, _forceMenuAtTopInFullscreenMode.Value)) return;
                _forceMenuAtTopInFullscreenMode.Value = value;
                OnPropertyChanged();
            }
        }

        #region App scale
        private bool _scaleLoaded;
        private double _scale;

        private void EnsureLoaded() {
            if (!_scaleLoaded) {
                _scaleLoaded = true;
                _scale = ValuesStorage.Get("__uiScale_2", 1d);
            }

            if (_scale < 0.1 || _scale > 4d || double.IsNaN(_scale) || double.IsInfinity(_scale)) {
                _scale = 1d;
            }
        }

        public double AppScale {
            get {
                EnsureLoaded();
                return _scale;
            }
            set {
                EnsureLoaded();
                value = Math.Min(Math.Max(value, 0.2), 10d);
                if (Equals(value, _scale)) return;
                _scale = value;
                OnPropertyChanged();
                ValuesStorage.Set("__uiScale_2", value);

                foreach (var window in Application.Current.Windows.OfType<DpiAwareWindow>()) {
                    window.SetAppScale(value);
                }
            }
        }
        #endregion
    }
}
