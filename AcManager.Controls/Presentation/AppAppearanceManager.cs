using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    public class AppAppearanceManager : NotifyPropertyChanged {
        public static bool OptionCustomThemes = false;
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
        public const string KeyShowSubMenuDraggableIcons = "AppAppearanceManager.ShowSubMenuDraggableIcons";

        public const string UriDefaultTheme = "/AcManager.Controls;component/Assets/ModernUI.AcTheme.xaml";
        public const string UriBlackTheme = "/AcManager.Controls;component/Assets/ModernUI.Black.xaml";

        public static AppAppearanceManager Instance { get; private set; }

        public static AppAppearanceManager Initialize() {
            if (Instance != null) throw new Exception(@"Already initialized");
            Instance = new AppAppearanceManager();
            Instance.InnerInitialize();
            return Instance;
        }

        private string _themeError;

        public string ThemeError {
            get { return _themeError; }
            internal set {
                if (Equals(value, _themeError)) return;
                _themeError = value;
                OnPropertyChanged();
            }
        }

        public sealed class ThemeLink : Link, IWithId {
            public ThemeLink(string name, Uri source) {
                DisplayName = name;
                Source = source;

                Id = source.ToString();
            }

            private readonly string _filename;
            private DateTime _modified;

            public ThemeLink(string filename) {
                DisplayName = Path.GetFileNameWithoutExtension(filename);
                Source = new Uri($"file://{filename}", UriKind.Absolute);

                _filename = filename;
                Id = Path.GetFileName(filename);
                AssetsDirectory = Path.GetDirectoryName(_filename);
            }

            public string Id { get; }

            private void ApplyInner() {
                if (_filename != null) {
                    var parserContext = new ParserContext {
                        BaseUri = new Uri(AssetsDirectory ?? "", UriKind.Absolute)
                    };

                    using (var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        AppearanceManager.Current.SetTheme((ResourceDictionary)XamlReader.Load(fs, parserContext));
                    }
                } else {
                    AppearanceManager.Current.SetTheme(Source);
                }
            }

            [CanBeNull]
            public string AssetsDirectory { get; }

            public bool Apply() {
                try {
                    ApplyInner();
                    if (_filename != null) {
                        _modified = new FileInfo(_filename).LastWriteTime;
                    }

                    Instance.ThemeError = null;
                    return true;
                } catch (Exception e) {
                    Logging.Warning(e);
                    Instance.ThemeError = e.Message;
                    return false;
                }
            }

            public bool Update() {
                if (_filename == null) return false;

                var fileInfo = new FileInfo(_filename);
                if (!fileInfo.Exists) return false;

                try {
                    var modified = fileInfo.LastWriteTime;
                    if (modified > _modified) {
                        ApplyInner();
                        _modified = modified;
                    }

                    Instance.ThemeError = null;
                    return true;
                } catch (Exception e) {
                    Logging.Warning(e);
                    Instance.ThemeError = e.Message;
                    return false;
                }
            }
        }

        private bool _loading;

        private AppAppearanceManager() {}

        private void InnerInitialize() {
            AppearanceManager.Current.Initialize();
            AppearanceManager.Current.ThemeObsolete += OnThemeObsolete;

            var theme = ValuesStorage.GetString(KeyTheme);
            InitializeThemesList();
            SelectedTheme = Themes.OfType<ThemeLink>().GetByIdOrDefault(theme) ?? Themes.OfType<ThemeLink>().FirstOrDefault();

            try {
                _loading = true;
                AccentColor = ValuesStorage.GetColor(KeyAccentColor, AccentColors.First());
                if (AccentColor.A == 0) AccentColor = AccentColors.First();

                AccentDisplayColor = ValuesStorage.GetString(KeyAccentDisplayColor);
                IdealFormattingMode = ValuesStorage.GetBool(KeyIdealFormattingMode, OptionIdealFormattingModeDefaultValue);
                SmallFont = ValuesStorage.GetBool(KeySmallFont);
                BitmapScalingMode = ValuesStorage.GetEnum(KeyBitmapScaling, BitmapScalingMode.HighQuality);
                LargeSubMenuFont = ValuesStorage.GetBool(KeyLargeSubMenuFont);
                ShowSubMenuDraggableIcons = ValuesStorage.GetBool(KeyShowSubMenuDraggableIcons, true);
                PopupToolBars = ValuesStorage.GetBool(KeyPopupToolBars);
                FrameAnimation = FrameAnimations.FirstOrDefault(x => x.Id == ValuesStorage.GetString(KeyFrameAnimation)) ?? FrameAnimations.First();
            } finally {
                _loading = false;
            }
        }

        private void OnThemeObsolete(object sender, EventArgs e) {
            SelectedTheme?.Apply();
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
                    AppearanceManager.Current.IdealFormattingMode = value;
                    return;
                }

                if (Equals(value, _idealFormattingMode)) return;
                _idealFormattingMode = value;
                OnPropertyChanged();
                AppearanceManager.Current.IdealFormattingMode = value;
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

        private bool _showSubMenuDraggableIcons;

        public bool ShowSubMenuDraggableIcons {
            get { return _showSubMenuDraggableIcons; }
            set {
                if (_loading) {
                    _showSubMenuDraggableIcons = value;
                    AppearanceManager.Current.SubMenuDraggablePoints = value;
                    return;
                }

                if (Equals(value, _showSubMenuDraggableIcons)) return;
                _showSubMenuDraggableIcons = value;
                OnPropertyChanged();
                AppearanceManager.Current.SubMenuDraggablePoints = value;
                ValuesStorage.Set(KeyShowSubMenuDraggableIcons, value);
            }
        }
        #endregion
        
        #region Theme and color
        public Color[] AccentColors { get; } = {
            Color.FromRgb(0xa2, 0x00, 0x25), // nordschleife special
            Color.FromRgb(0x33, 0x99, 0xff), // blue
            Color.FromRgb(0x00, 0xab, 0xa9), // teal
            Color.FromRgb(0x33, 0x99, 0x33), // green
            Color.FromRgb(0x8c, 0xbf, 0x26), // lime
            Color.FromRgb(0xf0, 0x96, 0x09), // orange
            Color.FromRgb(0xff, 0x45, 0x00), // orange red
            Color.FromRgb(0xe5, 0x14, 0x00), // red
            Color.FromRgb(0xff, 0x00, 0x97), // magenta
            Color.FromRgb(0xa2, 0x00, 0xff), // purple            
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

        private static readonly ThemeLink[] BuiltInThemes = {
            new ThemeLink(ControlsStrings.Theme_Nordschleife, new Uri(UriDefaultTheme, UriKind.Relative)),
            new ThemeLink(ControlsStrings.Theme_Dark, AppearanceManager.DarkThemeSource),
            new ThemeLink(ControlsStrings.Theme_Black, new Uri(UriBlackTheme, UriKind.Relative)),
            new ThemeLink(ControlsStrings.Theme_Light, AppearanceManager.LightThemeSource)
        };

        public HierarchicalGroup Themes { get; } = new HierarchicalGroup();

        private void InitializeThemesList() {
            if (OptionCustomThemes) {
                UpdateThemesList();
                FilesStorage.Instance.Watcher(FilesStorage.Instance.GetDirectory("Themes")).Update += (sender, args) => {
                    UpdateThemesList();
                };
            } else {
                Themes.ReplaceEverythingBy(BuiltInThemes);
            }
        }

        private void UpdateThemesList() {
            var dir = FilesStorage.Instance.GetDirectory("Themes");
            var files = FileUtils.GetFilesSafe(dir, "*.xaml");
            Themes.ReplaceEverythingBy(files.Any()
                    ? BuiltInThemes.OfType<object>().Append(new Separator()).Concat(files.Select(x => new ThemeLink(x)))
                    : BuiltInThemes);
            SelectedTheme?.Update();
        }

        private ThemeLink _selectedTheme;

        public ThemeLink SelectedTheme {
            get { return _selectedTheme; }
            set {
                if (value == null || Equals(value, _selectedTheme)) return;
                if (value.Apply()) {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyTheme, value.Id);
                } else {
                    SelectedTheme = BuiltInThemes.First();
                }
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

                var window = Application.Current?.MainWindow;
                if (window == null) return;
                foreach (var child in window.FindVisualChildren<ModernFrame>()) {
                    child.TransitionName = value.Id;
                }
            }
        }
        #endregion
    }
}
