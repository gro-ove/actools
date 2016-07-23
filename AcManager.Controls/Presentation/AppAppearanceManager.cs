using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Presentation {
    public class AppAppearanceManager : NotifyPropertyChanged {
        public static bool OptionIdealFormattingModeDefaultValue = false;

        public const string KeyTheme = "appearance_theme";
        public const string KeyAccentColor = "appearance_accentColor";
        public const string KeyAccentDisplayColor = "appearance_accentColor_d";
        public const string KeyIdealFormattingMode = "appearance_idealFormattingMode";
        public const string KeySmallFont = "appearance_smallFont";
        public const string KeyBitmapScaling = "appearance_bitmapScaling";
        public const string KeyPopupToolBars = "appearance_popupToolBars";
        public const string KeyFrameAnimation = "AppAppearanceManager.FrameAnimation";
        public const string KeyLargeSubMenuFont = "AppAppearanceManager.LargeSubMenuFont";

        public const string UriDefaultTheme = "/AcManager.Controls;component/Assets/ModernUI.AcTheme.xaml";

        public static AppAppearanceManager Instance { get; private set; }

        public static AppAppearanceManager Initialize() {
            if (Instance != null) throw new Exception(@"Already initialized");
            return Instance = new AppAppearanceManager();
        }

        private readonly bool _loading;

        private AppAppearanceManager() {
            AppearanceManager.Current.Initialize();

            var theme = ValuesStorage.GetUri(KeyTheme);
            SelectedTheme = Themes.FirstOrDefault(x => x.Source == theme) ?? Themes.FirstOrDefault();

            try {
                _loading = true;
                AccentColor = ValuesStorage.GetColor(KeyAccentColor, AccentColors.First());
                AccentDisplayColor = ValuesStorage.GetString(KeyAccentDisplayColor);
                IdealFormattingMode = ValuesStorage.GetBool(KeyIdealFormattingMode, OptionIdealFormattingModeDefaultValue);
                SmallFont = ValuesStorage.GetBool(KeySmallFont);
                BitmapScalingMode = ValuesStorage.GetEnum(KeyBitmapScaling, BitmapScalingMode.HighQuality);
                LargeSubMenuFont = ValuesStorage.GetBool(KeyLargeSubMenuFont);
                PopupToolBars = ValuesStorage.GetBool(KeyPopupToolBars);
                FrameAnimation = FrameAnimations.FirstOrDefault(x => x.Id == ValuesStorage.GetString(KeyFrameAnimation)) ?? FrameAnimations.First();
            } finally {
                _loading = false;
            }
        }

        #region Bitmap scaling
        private BitmapScalingMode _bitmapScalingMode;

        public BitmapScalingMode BitmapScalingMode {
            get { return _bitmapScalingMode; }
            set {
                if (_loading) {
                    _bitmapScalingMode = value;
                    RenderOptions.BitmapScalingModeProperty.OverrideMetadata(typeof(BetterImage), new FrameworkPropertyMetadata(_bitmapScalingMode));
                    return;
                }

                if (Equals(value, _bitmapScalingMode)) return;
                _bitmapScalingMode = value;
                OnPropertyChanged();
                ValuesStorage.SetEnum(KeyBitmapScaling, value);
            }
        }
        #endregion

        #region Font sizes
        private bool _idealFormattingMode;

        public bool IdealFormattingMode {
            get { return _idealFormattingMode; }
            set {
                if (_loading) {
                    _idealFormattingMode = value;
                    AppearanceManager.Current.OptionIdealFormattingMode = value;
                    return;
                }

                if (Equals(value, _idealFormattingMode)) return;
                _idealFormattingMode = value;
                OnPropertyChanged();
                AppearanceManager.Current.OptionIdealFormattingMode = value;
                ValuesStorage.Set(KeyIdealFormattingMode, value);
            }
        }

        private bool _smallFont;

        public bool SmallFont {
            get { return _smallFont; }
            set {
                if (_loading) {
                    _smallFont = value;
                    AppearanceManager.Current.FontSize = value ? FontSize.Small : FontSize.Large;
                    return;
                }

                if (Equals(value, _smallFont)) return;
                _smallFont = value;
                OnPropertyChanged();
                AppearanceManager.Current.FontSize = value ? FontSize.Small : FontSize.Large;
                ValuesStorage.Set(KeySmallFont, value);
            }
        }

        private bool _largeSubMenuFont;

        public bool LargeSubMenuFont {
            get { return _largeSubMenuFont; }
            set {
                if (_loading) {
                    _largeSubMenuFont = value;
                    AppearanceManager.Current.SubMenuFontSize = value ? FontSize.Large : FontSize.Small;
                    return;
                }

                if (Equals(value, _largeSubMenuFont)) return;
                _largeSubMenuFont = value;
                OnPropertyChanged();
                AppearanceManager.Current.SubMenuFontSize = value ? FontSize.Large : FontSize.Small;
                ValuesStorage.Set(KeyLargeSubMenuFont, value);
            }
        }
        #endregion
        
        #region Theme and color
        public Color[] AccentColors { get; } = {
            Color.FromArgb(0xff, 0xa2, 0x00, 0x25), // nordschleife special
            Color.FromArgb(0xff, 0x33, 0x99, 0xff), // blue
            Color.FromArgb(0xff, 0x00, 0xab, 0xa9), // teal
            Color.FromArgb(0xff, 0x33, 0x99, 0x33), // green
            Color.FromArgb(0xff, 0x8c, 0xbf, 0x26), // lime
            Color.FromArgb(0xff, 0xf0, 0x96, 0x09), // orange
            Color.FromArgb(0xff, 0xff, 0x45, 0x00), // orange red
            Color.FromArgb(0xff, 0xe5, 0x14, 0x00), // red
            Color.FromArgb(0xff, 0xff, 0x00, 0x97), // magenta
            Color.FromArgb(0xff, 0xa2, 0x00, 0xff), // purple            
        };

        private Color _accentColor;

        public Color AccentColor {
            get { return _accentColor; }
            set {
                if (_loading) {
                    _accentColor = value;
                    AppearanceManager.Current.SetAccentColorAsync(value);
                    return;
                }

                if (Equals(value, _accentColor)) return;
                _accentColor = value;
                OnPropertyChanged();
                AppearanceManager.Current.SetAccentColorAsync(value);
                ValuesStorage.Set(KeyAccentColor, value);
            }
        }

        private string _accentDisplayColor;

        public string AccentDisplayColor {
            get { return _accentDisplayColor; }
            set {
                if (_loading) {
                    _accentDisplayColor = value;
                    return;
                }

                if (Equals(value, _accentDisplayColor)) return;
                _accentDisplayColor = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyAccentDisplayColor, value);
            }
        }

        public Link[] Themes { get; } = {
            new Link {
                DisplayName = ControlsStrings.Theme_Nordschleife,
                Source = new Uri(UriDefaultTheme, UriKind.Relative)
            },
            new Link {
                DisplayName = ControlsStrings.Theme_Dark,
                Source = AppearanceManager.DarkThemeSource
            },
            new Link {
                DisplayName = ControlsStrings.Theme_Light,
                Source = AppearanceManager.LightThemeSource
            }
        };

        private Link _selectedTheme;

        public Link SelectedTheme {
            get { return _selectedTheme; }
            set {
                if (value == null || Equals(value, _selectedTheme)) return;
                _selectedTheme = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyTheme, value.Source);
                AppearanceManager.Current.ThemeSource = value.Source;
            }
        }
        #endregion
        
        #region Toolbars
        private bool? _popupToolBars;

        public bool PopupToolBars {
            get { return _popupToolBars ?? false; }
            set {
                if (_loading) {
                    AppearanceManager.Current.PopupToolBars = value;
                    _popupToolBars = value;
                    return;
                }

                if (Equals(value, _popupToolBars)) return;
                _popupToolBars = value;
                OnPropertyChanged();
                AppearanceManager.Current.PopupToolBars = value;
                ValuesStorage.Set(KeyPopupToolBars, value);
            }
        }
        #endregion
        
        #region Transitions

        public class FrameAnimationEntry {
            public string DisplayName { get; }

            public string Id { get; }

            public FrameAnimationEntry([Localizable(false)] string id, string displayName) {
                Id = id;
                DisplayName = displayName;
            }
        }

        public FrameAnimationEntry[] FrameAnimations { get; } = {
            new FrameAnimationEntry("Normal", Tools.ToolsStrings.Common_Disabled),
            new FrameAnimationEntry("ModernUITransition", ControlsStrings.Animation_Modern),
            new FrameAnimationEntry("DefaultTransition", ControlsStrings.Animation_Fade),
            new FrameAnimationEntry("UpTransition", ControlsStrings.Animation_Up),
            new FrameAnimationEntry("DownTransition", ControlsStrings.Animation_Down)
        };

        private FrameAnimationEntry _frameAnimation;

        public FrameAnimationEntry FrameAnimation {
            get { return _frameAnimation; }
            set {
                if (value == null) return;

                if (_loading) {
                    ModernFrame.OptionTransitionName = value.Id;
                    _frameAnimation = value;
                    return;
                }

                if (Equals(value, _frameAnimation)) return;
                _frameAnimation = value;
                OnPropertyChanged();

                ValuesStorage.Set(KeyFrameAnimation, value.Id);
                ModernFrame.OptionTransitionName = value.Id;

                if (Application.Current.MainWindow == null) return;
                foreach (var child in Application.Current.MainWindow.FindVisualChildren<ModernFrame>()) {
                    child.TransitionName = value.Id;
                }
            }
        }
        #endregion
    }
}
