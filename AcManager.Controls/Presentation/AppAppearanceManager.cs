using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    public class AppAppearanceManager : NotifyPropertyChanged {
        public static bool OptionCustomThemes = true;

        public const string KeyTheme = "appearance_theme";
        public const string KeyAccentColor = "appearance_accentColor";
        public const string KeyAccentDisplayColor = "appearance_accentColor_d";
        public const string KeyIdealFormattingMode = "appearance_idealFormattingMode_2";
        public const string KeyBlurImageViewerBackground = "appearance_blurImageViewerBackground";
        public const string KeyDisallowTransparency = "appearance_disallowTransparency";
        public const string KeyHideImageViewerButtons = "appearance_hideImageViewerButtons";
        public const string KeySmallFont = "appearance_smallFont";
        public const string KeyLargerTitleLinks = "appearance_biggerTitleLinks";
        public const string KeyBoldTitleLinks = "appearance_boldTitleLinks";
        public const string KeyForceMenuAtTopInFullscreenMode = "appearance_forceMenuAtTopInFullscreenMode";
        public const string KeyBackgroundImage = "appearance_backgroundImage";
        public const string KeyBackgroundOpacity = "appearance_backgroundImageOpacity";
        public const string KeyBackgroundBlur = "appearance_backgroundImageBlur";
        public const string KeySlideshowChangeRate = "appearance_slideshowChangeRate";
        public const string KeyBackgroundStretch = "appearance_backgroundImageStretch";
        public const string KeySoftwareRendering = "appearance_softwareRendering";
        public const string KeyBitmapScaling = "appearance_bitmapScaling";
        public const string KeyPopupToolBars = "appearance_popupToolBars";
        public const string KeyFrameAnimation = "AppAppearanceManager.FrameAnimation";
        public const string KeyLargeSubMenuFont = "AppAppearanceManager.LargeSubMenuFont";
        public const string KeyShowSubMenuDraggableIcons = "AppAppearanceManager.ShowSubMenuDraggableIcons";

        public const string UriDefaultTheme = "/AcManager.Controls;component/Assets/ModernUI.AcTheme.xaml";
        public const string UriBlackTheme = "/AcManager.Controls;component/Assets/ModernUI.Black.xaml";
        public const string UriLightTheme = "/AcManager.Controls;component/Assets/ModernUI.Light.xaml";
        public const string UriWhiteTheme = "/AcManager.Controls;component/Assets/ModernUI.White.xaml";

        public static AppAppearanceManager Instance { get; private set; }

        public static AppAppearanceManager Initialize(TitleLinkEnabledEntry[] titleLinkEnabledEntries) {
            if (Instance != null) throw new Exception(@"Already initialized");
            Instance = new AppAppearanceManager(titleLinkEnabledEntries);
            Instance.InnerInitialize();
            return Instance;
        }

        private string _themeError;

        public string ThemeError {
            get => _themeError;
            internal set => Apply(value, ref _themeError);
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
                        AppearanceManager.Instance.SetTheme((ResourceDictionary)XamlReader.Load(fs, parserContext));
                    }
                } else {
                    AppearanceManager.Instance.SetTheme(Source);
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

        private AppAppearanceManager(TitleLinkEnabledEntry[] titleLinkEnabledEntries) {
            TitleLinkEntries = titleLinkEnabledEntries;
        }

        private void InnerInitialize() {
            AppearanceManager.Instance.Initialize();
            AppearanceManager.Instance.ThemeObsolete += OnThemeObsolete;

            var theme = ValuesStorage.Get<string>(KeyTheme);

            // For compatibility with old settings
            if (theme == @"/FirstFloor.ModernUI;component/Assets/ModernUI.Light.xaml") {
                theme = @"/AcManager.Controls;component/Assets/ModernUI.Light.xaml";
            }

            InitializeThemesList();
            SelectedTheme = Themes.OfType<ThemeLink>().GetByIdOrDefault(theme) ?? Themes.OfType<ThemeLink>().FirstOrDefault();

            try {
                _loading = true;
                var accentColor = ValuesStorage.Get(KeyAccentColor, AccentColors.First());
                Logging.Write($"Accent color: {accentColor}");
                AccentColor = accentColor.A == 0 ? AccentColors.First() : accentColor;

                AccentDisplayColor = ValuesStorage.Get<string>(KeyAccentDisplayColor);
                BackgroundFilename = ValuesStorage.Get<string>(KeyBackgroundImage);
                BackgroundOpacity = ValuesStorage.Get(KeyBackgroundOpacity, 0.2);
                BackgroundBlur = ValuesStorage.Get(KeyBackgroundBlur, 0d);
                BackgroundStretch = ValuesStorage.Get(KeyBackgroundStretch, Stretch.UniformToFill);
                SlideshowChangeRate = ValuesStorage.Get(KeySlideshowChangeRate, SlideshowChangeRates.ElementAt(3).Value);
                IdealFormattingMode = ValuesStorage.Get<bool?>(KeyIdealFormattingMode);
                BlurImageViewerBackground = ValuesStorage.Get<bool>(KeyBlurImageViewerBackground);
                DisallowTransparency = ValuesStorage.Get<bool>(KeyDisallowTransparency);
                SmallFont = ValuesStorage.Get<bool>(KeySmallFont);
                LargerTitleLinks = ValuesStorage.Get<bool>(KeyLargerTitleLinks);
                BoldTitleLinks = ValuesStorage.Get<bool>(KeyBoldTitleLinks);
                BitmapScalingMode = ValuesStorage.Get(KeyBitmapScaling, BitmapScalingMode.HighQuality);
                SoftwareRenderingMode = ValuesStorage.Get<bool>(KeySoftwareRendering);
                LargeSubMenuFont = ValuesStorage.Get<bool>(KeyLargeSubMenuFont);
                ShowSubMenuDraggableIcons = ValuesStorage.Get(KeyShowSubMenuDraggableIcons, true);
                PopupToolBars = ValuesStorage.Get<bool>(KeyPopupToolBars);
                FrameAnimation = FrameAnimations.GetByIdOrDefault(ValuesStorage.Get<string>(KeyFrameAnimation)) ?? FrameAnimations.First();

                UpdateBackgroundImageBrush(false);
            } finally {
                _loading = false;
            }
        }

        private void OnThemeObsolete(object sender, EventArgs e) {
            SelectedTheme?.Apply();
        }

        #region Background
        private Regex _slideshowFilter;
        private List<string> _slideshowFiles;
        private GoodShuffle<string> _slideshowShuffle;
        private CancellationTokenSource _slideshowWaitCancellation;
        private string _slideshowLast;

        private async Task StartSlideshowTimer() {
            _slideshowWaitCancellation?.Cancel();
            if (SlideshowChangeRate == TimeSpan.MaxValue
                    || SlideshowChangeRate.TotalSeconds < 3d) {
                return;
            }
            using (var c = new CancellationTokenSource()) {
                _slideshowWaitCancellation = c;
                await Task.Delay(SlideshowChangeRate, c.Token);
                if (_slideshowWaitCancellation == c) {
                    _slideshowWaitCancellation = null;
                    if (SlideshowMode) {
                        UpdateBackgroundImageBrush(false);
                    }
                }
            }
        }

        private async Task UpdateBackgroundImageBrushUI(bool keepSlideshow) {
            BetterImage.Image image;
            if (_backgroundFilename == null) {
                image = BetterImage.Image.Empty;
            } else if (SlideshowMode) {
                if (_slideshowFilter == null) {
                    _slideshowFilter = new Regex(@"\.(?:bmp|png|jpe?g|gif)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }

                var files = await Task.Run(() => FileUtils.GetFilesRecursive(_backgroundFilename.TrimEnd('\\'))
                        .Where(x => _slideshowFilter.IsMatch(x)).OrderBy(x => x).ToList());
                if (_slideshowFiles == null || !files.SequenceEqual(_slideshowFiles)) {
                    _slideshowFiles = files;
                    _slideshowShuffle = GoodShuffle.Get(_slideshowFiles);
                    _slideshowLast = null;
                }

                _slideshowLast = (keepSlideshow ? _slideshowLast : null) ?? _slideshowShuffle.Next;
                image = await BetterImage.LoadBitmapSourceAsync(_slideshowLast);
                StartSlideshowTimer().Ignore();
            } else {
                image = await BetterImage.LoadBitmapSourceAsync(_backgroundFilename);
            }

            Brush brush;
            if (image.ImageSource == null) {
                brush = null;
            } else if (_backgroundBlur <= 0.01d || _backgroundStretch == Stretch.None) {
                var imageBrush = new ImageBrush {
                    ImageSource = image.ImageSource,
                    Opacity = _backgroundOpacity,
                    Stretch = _backgroundStretch,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
                if (_backgroundStretch == Stretch.None) {
                    imageBrush.TileMode = TileMode.Tile;
                    imageBrush.Viewport = new Rect(0, 0, image.Width, image.Height);
                    imageBrush.ViewportUnits = BrushMappingMode.Absolute;
                }
                imageBrush.Freeze();
                brush = imageBrush;
            } else {
                brush = new VisualBrush {
                    Visual = new Border {
                        Child = new BetterImage {
                            ImageSource = image.ImageSource,
                            Stretch = _backgroundStretch,
                            Margin = new Thickness(-_backgroundBlur * 100d),
                            Effect = new BlurEffect { Radius = _backgroundBlur * 100d }
                        },
                        CacheMode = new BitmapCache(0.5d),
                        ClipToBounds = true
                    },
                    Opacity = _backgroundOpacity,
                    Stretch = _backgroundStretch,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            }

            Application.Current.Resources["WindowBackgroundContentBrush"] = brush;
        }

        private void UpdateBackgroundImageBrush(bool keepSlideshow) {
            ActionExtension.InvokeInMainThreadAsync(() => UpdateBackgroundImageBrushUI(keepSlideshow).Ignore());
        }

        private string _backgroundFilename;

        [CanBeNull]
        public string BackgroundFilename {
            get => _backgroundFilename;
            set {
                if (_loading) {
                    _backgroundFilename = value;
                    return;
                }

                if (Equals(value, _backgroundFilename)) return;
                _backgroundFilename = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SlideshowMode));
                ValuesStorage.Set(KeyBackgroundImage, value);
                UpdateBackgroundImageBrush(false);
            }
        }

        public bool SlideshowMode => BackgroundFilename?.EndsWith(@"\") == true;

        private double _backgroundBlur;

        public double BackgroundBlur {
            get => _backgroundBlur;
            set {
                if (_loading) {
                    _backgroundBlur = value;
                    return;
                }

                if (Equals(value, _backgroundBlur)) return;
                _backgroundBlur = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyBackgroundBlur, value);
                UpdateBackgroundImageBrush(true);
            }
        }

        private double _backgroundOpacity = 0.2;

        public double BackgroundOpacity {
            get => _backgroundOpacity;
            set {
                if (_loading) {
                    _backgroundOpacity = value;
                    return;
                }

                if (Equals(value, _backgroundOpacity)) return;
                _backgroundOpacity = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyBackgroundOpacity, value);
                UpdateBackgroundImageBrush(true);
            }
        }

        public SettingEntry<TimeSpan>[] SlideshowChangeRates { get; } = new[] {
            TimeSpan.FromSeconds(5d),
            TimeSpan.FromSeconds(30d),
            TimeSpan.FromMinutes(1d),
            TimeSpan.FromMinutes(3d),
            TimeSpan.FromMinutes(5d),
            TimeSpan.FromMinutes(10d),
            TimeSpan.FromMinutes(30d),
            TimeSpan.FromMinutes(120d),
        }.Select(x => new SettingEntry<TimeSpan>(x, x.ToReadableTime()))
                .Append(new SettingEntry<TimeSpan>(TimeSpan.MaxValue, "Only at launch")).ToArray();

        private TimeSpan _slideshowChangeRate;

        public TimeSpan SlideshowChangeRate {
            get => _slideshowChangeRate;
            set {
                if (_loading) {
                    _slideshowChangeRate = value;
                    return;
                }

                if (Equals(value, _slideshowChangeRate)) return;
                _slideshowChangeRate = value;
                ValuesStorage.Set(KeySlideshowChangeRate, value);
                OnPropertyChanged();
                StartSlideshowTimer().Ignore();
            }
        }

        public SettingEntry<TimeSpan> SlideshowChangeRateMode {
            get => SlideshowChangeRates.GetByIdOrDefault(SlideshowChangeRate);
            set => SlideshowChangeRate = value.Value;
        }

        public SettingEntry<Stretch>[] StretchModes { get; } = {
            new SettingEntry<Stretch>(Stretch.UniformToFill, "Fill"),
            new SettingEntry<Stretch>(Stretch.None, "Tile"),
            new SettingEntry<Stretch>(Stretch.Uniform, "Fit"),
            new SettingEntry<Stretch>(Stretch.Fill, "Stretch"),
        };

        public SettingEntry<Stretch> BackgroundStretchMode {
            get => StretchModes.GetByIdOrDefault(BackgroundStretch);
            set => BackgroundStretch = value.Value;
        }

        private Stretch _backgroundStretch;

        public Stretch BackgroundStretch {
            get => _backgroundStretch;
            set {
                if (_loading) {
                    _backgroundStretch = value;
                    return;
                }

                if (Equals(value, _backgroundStretch)) return;
                _backgroundStretch = value;
                ValuesStorage.Set(KeyBackgroundStretch, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundStretchMode));
                UpdateBackgroundImageBrush(true);
            }
        }
        #endregion

        #region Bitmap scaling
        private BitmapScalingMode _bitmapScalingMode;

        public BitmapScalingMode BitmapScalingMode {
            get => _bitmapScalingMode;
            set {
                if (_loading) {
                    _bitmapScalingMode = value;
                    RenderOptions.BitmapScalingModeProperty.OverrideMetadata(typeof(BetterImage), new FrameworkPropertyMetadata(_bitmapScalingMode));
                    return;
                }

                if (Equals(value, _bitmapScalingMode)) return;
                _bitmapScalingMode = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyBitmapScaling, value);
            }
        }
        #endregion

        #region Software rendering
        private bool _softwareRenderingMode;

        public bool SoftwareRenderingMode {
            get => _softwareRenderingMode;
            set {
                if (_loading) {
                    _softwareRenderingMode = value;
                    return;
                }

                if (Equals(value, _softwareRenderingMode)) return;
                _softwareRenderingMode = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeySoftwareRendering, value);
            }
        }
        #endregion

        #region Font sizes
        private bool? _idealFormattingMode;

        /// <summary>
        /// Null for automatic mode.
        /// </summary>
        public bool? IdealFormattingMode {
            get => _idealFormattingMode;
            set {
                if (_loading) {
                    _idealFormattingMode = value;
                    AppearanceManager.Instance.IdealFormattingMode = value;
                    return;
                }

                if (Equals(value, _idealFormattingMode)) return;
                _idealFormattingMode = value;
                OnPropertyChanged();
                AppearanceManager.Instance.IdealFormattingMode = value;
                if (value.HasValue) {
                    ValuesStorage.Set(KeyIdealFormattingMode, value.Value);
                } else {
                    ValuesStorage.Remove(KeyIdealFormattingMode);
                }
            }
        }

        private bool _blurImageViewerBackground;

        public bool BlurImageViewerBackground {
            get => _blurImageViewerBackground;
            set {
                if (Equals(value, _blurImageViewerBackground)) return;
                _blurImageViewerBackground = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyBlurImageViewerBackground, value);
            }
        }

        private bool _disallowTransparency;

        public bool DisallowTransparency {
            get => _disallowTransparency;
            set {
                if (Equals(value, _disallowTransparency)) return;
                _disallowTransparency = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyDisallowTransparency, value);
            }
        }

        private bool _hideImageViewerButtons;

        public bool HideImageViewerButtons {
            get => _hideImageViewerButtons;
            set {
                if (Equals(value, _hideImageViewerButtons)) return;
                _hideImageViewerButtons = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyHideImageViewerButtons, value);
            }
        }

        private bool _smallFont;

        public bool SmallFont {
            get => _smallFont;
            set {
                if (_loading) {
                    _smallFont = value;
                    AppearanceManager.Instance.FontSize = value ? FontSize.Small : FontSize.Large;
                    return;
                }

                if (Equals(value, _smallFont)) return;
                _smallFont = value;
                OnPropertyChanged();
                AppearanceManager.Instance.FontSize = value ? FontSize.Small : FontSize.Large;
                ValuesStorage.Set(KeySmallFont, value);
            }
        }

        private bool? _largerTitleLinks;

        public bool LargerTitleLinks {
            get => _largerTitleLinks ?? false;
            set {
                if (_loading) {
                    _largerTitleLinks = value;
                    AppearanceManager.Instance.LargerTitleLinks = value;
                    return;
                }

                if (Equals(value, _largerTitleLinks)) return;
                _largerTitleLinks = value;
                OnPropertyChanged();
                AppearanceManager.Instance.LargerTitleLinks = value;
                ValuesStorage.Set(KeyLargerTitleLinks, value);
            }
        }

        private bool? _boldTitleLinks;

        public bool BoldTitleLinks {
            get => _boldTitleLinks ?? false;
            set {
                if (_loading) {
                    _boldTitleLinks = value;
                    AppearanceManager.Instance.BoldTitleLinks = value;
                    return;
                }

                if (Equals(value, _boldTitleLinks)) return;
                _boldTitleLinks = value;
                OnPropertyChanged();
                AppearanceManager.Instance.BoldTitleLinks = value;
                ValuesStorage.Set(KeyBoldTitleLinks, value);
            }
        }

        private bool? _largeSubMenuFont;

        public bool LargeSubMenuFont {
            get => _largeSubMenuFont ?? false;
            set {
                if (_loading) {
                    _largeSubMenuFont = value;
                    AppearanceManager.Instance.SubMenuFontSize = value ? FontSize.Large : FontSize.Small;
                    return;
                }

                if (Equals(value, _largeSubMenuFont)) return;
                _largeSubMenuFont = value;
                OnPropertyChanged();
                AppearanceManager.Instance.SubMenuFontSize = value ? FontSize.Large : FontSize.Small;
                ValuesStorage.Set(KeyLargeSubMenuFont, value);
            }
        }

        private bool _showSubMenuDraggableIcons;

        public bool ShowSubMenuDraggableIcons {
            get => _showSubMenuDraggableIcons;
            set {
                if (_loading) {
                    _showSubMenuDraggableIcons = value;
                    AppearanceManager.Instance.SubMenuDraggablePoints = value;
                    return;
                }

                if (Equals(value, _showSubMenuDraggableIcons)) return;
                _showSubMenuDraggableIcons = value;
                OnPropertyChanged();
                AppearanceManager.Instance.SubMenuDraggablePoints = value;
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
            get => _accentColor;
            set {
                if (_loading) {
                    _accentColor = value;
                    AppearanceManager.Instance.SetAccentColorAsync(value);
                    return;
                }

                if (Equals(value, _accentColor)) return;
                _accentColor = value;
                OnPropertyChanged();
                AppearanceManager.Instance.SetAccentColorAsync(value);
                ValuesStorage.Set(KeyAccentColor, value);
            }
        }

        private string _accentDisplayColor;

        public string AccentDisplayColor {
            get => _accentDisplayColor;
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
            new ThemeLink(ControlsStrings.Theme_Light, new Uri(UriLightTheme, UriKind.Relative)),
            new ThemeLink("White", new Uri(UriWhiteTheme, UriKind.Relative)),
        };

        public HierarchicalGroup Themes { get; } = new HierarchicalGroup();

        private void InitializeThemesList() {
            if (OptionCustomThemes) {
                UpdateThemesList();
                FilesStorage.Instance.Watcher(FilesStorage.Instance.GetDirectory("Themes")).Update += (sender, args) => { UpdateThemesList(); };
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
            get => _selectedTheme;
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
            get => _popupToolBars ?? false;
            set {
                if (_loading) {
                    ToolbarsApparanceManager.PopupToolBars = value;
                    _popupToolBars = value;
                    return;
                }

                if (Equals(value, _popupToolBars)) return;
                _popupToolBars = value;
                OnPropertyChanged();
                ToolbarsApparanceManager.PopupToolBars = value;
                ValuesStorage.Set(KeyPopupToolBars, value);
            }
        }

        private static class ToolbarsApparanceManager {
            private static readonly Uri FixedToolBarsSource = new Uri("/AcManager.Controls;component/Assets/SelectedObjectToolBarTray/Fixed.xaml",
                    UriKind.Relative);

            private static readonly Uri PopupToolBarsSource = new Uri("/AcManager.Controls;component/Assets/SelectedObjectToolBarTray/Popup.xaml",
                    UriKind.Relative);

            private static ResourceDictionary _toolBarModeDictionary;

            public static bool? PopupToolBars {
                get => _toolBarModeDictionary == null ? (bool?)null : _toolBarModeDictionary.Source == PopupToolBarsSource;
                set {
                    if (Equals(value, PopupToolBars)) return;

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
        #endregion

        #region Transitions
        public class FrameAnimationEntry : IWithId {
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
            get => _frameAnimation;
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

        #region Miscellaneous
        public IReadOnlyList<TitleLinkEnabledEntry> TitleLinkEntries { get; }

        public bool? IsTitleLinkVisible(string key) {
            var link = TitleLinkEntries.GetByIdOrDefault(key);
            return link == null ? (bool?)null : link.IsAvailable && link.IsEnabled;
        }

        private readonly StoredValue<bool> _downloadsInSeparatePage = Stored.Get("AppAppearanceManager.DownloadsInSeparatePage", false);

        public bool DownloadsInSeparatePage {
            get => _downloadsInSeparatePage.Value;
            set => Apply(value, _downloadsInSeparatePage);
        }

        private readonly StoredValue<bool> _downloadsPageAutoOpen = Stored.Get("AppAppearanceManager.DownloadsPageAutoOpen", true);

        public bool DownloadsPageAutoOpen {
            get => _downloadsPageAutoOpen.Value;
            set => Apply(value, _downloadsPageAutoOpen);
        }

        private readonly StoredValue<bool> _semiTransparentAttachedTools = Stored.Get("AppAppearanceManager.SemiTransparentAttachedTools", false);

        public bool SemiTransparentAttachedTools {
            get => _semiTransparentAttachedTools.Value;
            set => Apply(value, _semiTransparentAttachedTools);
        }

        private readonly StoredValue<bool> _showMainWindowBackButton = Stored.Get("AppAppearanceManager.ShowMainWindowBackButton", true);

        public bool ShowMainWindowBackButton {
            get => _showMainWindowBackButton.Value;
            set => Apply(value, _showMainWindowBackButton);
        }
        #endregion
    }
}