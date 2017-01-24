using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class AppearanceManager : NotifyPropertyChanged {
        public static readonly Uri DarkThemeSource = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Dark.xaml", UriKind.Relative);
        public static readonly Uri LightThemeSource = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Light.xaml", UriKind.Relative);

        public static readonly Uri FixedToolBarsSource = new Uri("/AcManager.Controls;component/Assets/SelectedObjectToolBarTray/Fixed.xaml", UriKind.Relative);
        public static readonly Uri PopupToolBarsSource = new Uri("/AcManager.Controls;component/Assets/SelectedObjectToolBarTray/Popup.xaml", UriKind.Relative);

        public const string KeyAccentColor = "AccentColor";
        public const string KeyAccent = "Accent";
        public const string KeyDefaultFontSize = "DefaultFontSize";
        public const string KeyFormattingMode = "FormattingMode";
        public const string KeyFixedFontSize = "FixedFontSize";
        public const string KeySubMenuFontSize = "ModernSubMenuFontSize";
        public const string KeySubMenuDraggablePoints = "ModernSubMenuDraggablePoints";

        public event EventHandler ThemeChange;
        public event EventHandler ThemeObsolete;
        
        private AppearanceManager() {
        }

        public void Initialize() {
            /*Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                [@"DefaultFont"] = new FontFamily(@"Courier New"),
                [@"LightFont"] = new FontFamily(@"Impact"),
            });*/
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.xaml", UriKind.Relative)
            });
        }

        public static AppearanceManager Current { get; } = new AppearanceManager();

        private ResourceDictionary _currentThemeDictionary;

        [CanBeNull]
        public ResourceDictionary CurrentThemeDictionary {
            get { return _currentThemeDictionary; }
            private set {
                if (Equals(value, _currentThemeDictionary)) return;
                _currentThemeDictionary = value;
                OnPropertyChanged();
            }
        }

        public void SetTheme(ResourceDictionary dictionary) {
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

        public bool IdealFormattingMode {
            get { return Equals(Application.Current.Resources[KeyFormattingMode] as TextFormattingMode?, TextFormattingMode.Ideal); }
            set {
                Application.Current.Resources[KeyFormattingMode] = value ? TextFormattingMode.Ideal : TextFormattingMode.Display;
                OnPropertyChanged(nameof(IdealFormattingMode));
            }
        }

        public FontSize FontSize {
            get { return Equals(Application.Current.Resources[KeyDefaultFontSize] as double? ?? 0d, 12D) ? FontSize.Small : FontSize.Large; }
            set {
                if (FontSize == value) return;
                Application.Current.Resources[KeyDefaultFontSize] = value == FontSize.Small ? 12D : 13D;
                Application.Current.Resources[KeyFixedFontSize] = value == FontSize.Small ? 10.667D : 13.333D;
                OnPropertyChanged(nameof(FontSize));
            }
        }

        public FontSize SubMenuFontSize {
            get { return Equals(Application.Current.Resources[KeySubMenuFontSize] as double? ?? 0d, 11D) ? FontSize.Small : FontSize.Large; }
            set {
                if (SubMenuFontSize == value) return;
                Application.Current.Resources[KeySubMenuFontSize] = value == FontSize.Small ? 11D : 14D;
                OnPropertyChanged();
            }
        }

        public bool SubMenuDraggablePoints {
            get { return Equals(Application.Current.Resources[KeySubMenuDraggablePoints] as Visibility?, Visibility.Visible); }
            set {
                Application.Current.Resources[KeySubMenuDraggablePoints] = value ? Visibility.Visible : Visibility.Collapsed;
                OnPropertyChanged(nameof(SubMenuDraggablePoints));
            }
        }

        public Color AccentColor {
            get { return Application.Current.Resources[KeyAccentColor] as Color? ?? Color.FromArgb(0xff, 0x1b, 0xa1, 0xe2); }
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
                        await Task.Delay(300);
                    } while (_actualColor.HasValue); 
                    AccentColor = value;
                    _lastSet = DateTime.Now;
                } finally {
                    _settingInProgress = false;
                }
            }
        }

        private ResourceDictionary _toolBarModeDictionary;

        public bool? PopupToolBars {
            get { return _toolBarModeDictionary == null ? (bool?)null : _toolBarModeDictionary.Source == PopupToolBarsSource; }
            set {
                if (Equals(value, PopupToolBars)) return;
                OnPropertyChanged();

                if (_toolBarModeDictionary != null) {
                    Application.Current.Resources.MergedDictionaries.Remove(_toolBarModeDictionary);
                    _toolBarModeDictionary = null;
                }

                if (!value.HasValue) return;
                _toolBarModeDictionary = new ResourceDictionary { Source = value.Value ? PopupToolBarsSource : FixedToolBarsSource };
                Application.Current.Resources.MergedDictionaries.Add(_toolBarModeDictionary);
            }
        }
    }
}
