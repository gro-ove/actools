using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Presentation {
    public class AppAppearanceManager : NotifyPropertyChanged {
        public const string KeyTheme = "appearance_theme";
        public const string KeyAccentColor = "appearance_accentColor";
        public const string KeySmallFont = "appearance_smallFont";
        public const string KeyPopupToolBars = "appearance_ThemePopupToolBars";
        public const string KeyFrameAnimation = "AppAppearanceManager.FrameAnimation";
        public const string KeyLargeSubMenuFont = "AppAppearanceManager.LargeSubMenuFont";

        public const string UriDefaultTheme = "/AcManager.Controls;component/Assets/ModernUI.AcTheme.xaml";

        public static AppAppearanceManager Instance { get; private set; }

        public static AppAppearanceManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new AppAppearanceManager();
        }

        private AppAppearanceManager() {
            AppearanceManager.Current.Initialize();

            var theme = ValuesStorage.GetUri(KeyTheme);
            SelectedTheme = Themes.FirstOrDefault(x => x.Source == theme) ?? Themes.FirstOrDefault();

            AccentColor = ValuesStorage.GetColor(KeyAccentColor) ?? AccentColors.First();
            SmallFont = ValuesStorage.GetBool(KeySmallFont);
            LargeSubMenuFont = ValuesStorage.GetBool(KeyLargeSubMenuFont);
            PopupToolBars = ValuesStorage.GetBool(KeyPopupToolBars);
            FrameAnimation = FrameAnimations.FirstOrDefault(x => x.Id == ValuesStorage.GetString(KeyFrameAnimation)) ?? FrameAnimations.First();
        }

        #region Font sizes
        private bool _smallFont;

        public bool SmallFont {
            get { return _smallFont; }
            set {
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
                if (Equals(value, _accentColor)) return;
                _accentColor = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyAccentColor, value);
                AppearanceManager.Current.AccentColor = value;
            }
        }

        public Link[] Themes { get; } = {
            new Link {
                DisplayName = "Nordschleife",
                Source = new Uri(UriDefaultTheme, UriKind.Relative)
            },
            new Link {
                DisplayName = "Dark",
                Source = AppearanceManager.DarkThemeSource
            },
            new Link {
                DisplayName = "Light",
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

        public bool? PopupToolBars {
            get { return _popupToolBars; }
            set {
                if (Equals(value, _popupToolBars)) return;
                _popupToolBars = value;
                OnPropertyChanged();
                AppearanceManager.Current.PopupToolBars = value;
                ValuesStorage.Set(KeyPopupToolBars, value ?? false);
            }
        }
        #endregion
        
        #region Transitions

        public class FrameAnimationEntry {
            public string DisplayName { get; }

            public string Id { get; }

            public FrameAnimationEntry(string id, string displayName) {
                Id = id;
                DisplayName = displayName;
            }
        }

        public FrameAnimationEntry[] FrameAnimations { get; } = {
            new FrameAnimationEntry("Normal", "Disabled"),
            new FrameAnimationEntry("ModernUITransition", "Modern UI"),
            new FrameAnimationEntry("DefaultTransition", "Fade"),
            new FrameAnimationEntry("UpTransition", "Up"),
            new FrameAnimationEntry("DownTransition", "Down")
        };

        private FrameAnimationEntry _frameAnimation;

        public FrameAnimationEntry FrameAnimation {
            get { return _frameAnimation; }
            set {
                if (value == null) return;
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

    internal static class DependencyObjectExtension {
        public static IEnumerable<T> FindVisualChildren<T>([NotNull] this DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) throw new ArgumentNullException(nameof(depObj));

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var children = child as T;
                if (children != null) {
                    yield return children;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child)) {
                    yield return childOfChild;
                }
            }
        }
    }
}
