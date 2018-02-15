using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public static readonly DependencyProperty LocationAndSizeKeyProperty = DependencyProperty.Register(nameof(LocationAndSizeKey), typeof(string),
                typeof(DpiAwareWindow), new PropertyMetadata(null, (o, e) => {
                    var d = (DpiAwareWindow)o;
                    d.Logging.Debug("Location and size key: " + e.NewValue);
                    d._locationAndSizeKey = (string)e.NewValue;
                }));

        private string _locationAndSizeKey;

        public string LocationAndSizeKey {
            get => _locationAndSizeKey;
            set => SetValue(LocationAndSizeKeyProperty, value);
        }

        private class Placement {
            public int Left, Top, Width, Height;
            public double ScaleX = 1d, ScaleY = 1d;
            public bool IsMaximized;

            public static Placement FromString(string s) {
                return s.Split(';').Select(l => l.Split(new[] { '=' }, 2)).Aggregate(new Placement(), (r, a) => {
                    var f = a.Length == 2 ? r.GetType().GetField(a[0].Trim()) : null;
                    f?.SetValue(r, a[1].As(f.FieldType, f.GetValue(r)));
                    return r;
                });
            }

            public override string ToString() {
                return string.Join(@";", GetType().GetFields().Select(p => p.Name + '=' + p.GetValue(this).As<string>()));
            }
        }

        static DpiAwareWindow() {
            SimpleSerialization.Register(Placement.FromString);

            Helpers.Logging.Warning("Initializing DPI-aware windows…");
            Helpers.Logging.Warning($"Main screen: {Screen.PrimaryScreen}");
            foreach (var screen in Screen.AllScreens) {
                Helpers.Logging.Warning($"Found screen: {screen}");
            }
        }

        private void SetDefaultLocation() {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            var screen = GetPreferredScreen(this);
            Logging.Debug($"Screen to use: {screen.WorkingArea}");

            var originalState = WindowState;
            if (originalState != WindowState.Normal) {
                Logging.Debug("Roll back to normal state to move properly");
                WindowState = WindowState.Normal;
            }

            if (ActualWidth > screen.WorkingArea.Width) {
                // Do not change DPI-related original values, so, if window will be moved
                // somewhere else, to a window with different DPI, original values will be
                // restored
                if (MinWidth > screen.WorkingArea.Width) {
                    Logging.Debug($"Clamp min width to: {screen.WorkingArea.Width}");
                    SaveOriginalLimitations();
                    MinWidth = screen.WorkingArea.Width;
                }

                Logging.Debug($"Clamp width to: {screen.WorkingArea.Width}");
                Width = screen.WorkingArea.Width;
            }

            if (ActualHeight > screen.WorkingArea.Height) {
                if (MinHeight > screen.WorkingArea.Height) {
                    Logging.Debug($"Clamp min height to: {screen.WorkingArea.Height}");
                    SaveOriginalLimitations();
                    MinHeight = screen.WorkingArea.Height;
                }

                Logging.Debug($"Clamp height to: {screen.WorkingArea.Height}");
                Height = screen.WorkingArea.Height;
            }

            UpdatePreferredFullscreenMode(screen, originalState != WindowState.Normal, true);
        }

        protected virtual void OnInitializedOverride() { }

        protected sealed override void OnInitialized(EventArgs e) {
            Logging.Here();
            base.OnInitialized(e);
            OnInitializedOverride();
        }

        public static readonly DependencyProperty ConsiderPreferredFullscreenProperty = DependencyProperty.Register(nameof(ConsiderPreferredFullscreen),
                typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(true, (o, e) => { ((DpiAwareWindow)o)._considerPreferredFullscreen = (bool)e.NewValue; }));

        private bool _considerPreferredFullscreen = true;

        public bool ConsiderPreferredFullscreen {
            get => _considerPreferredFullscreen;
            set => SetValue(ConsiderPreferredFullscreenProperty, value);
        }

        private bool _locationAndSizeInitialized;

        private void LoadLocationAndSize() {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            Logging.Debug($"LS init-ed: {_locationAndSizeInitialized}, busy: {_locationAndSizeBusy.Is}");

            _locationAndSizeInitialized = true;
            _locationAndSizeBusy.DoDelayAfterwards(() => {
                if (LocationAndSizeKey == null) {
                    Logging.Debug("LS key not set, setting default location…");
                    SetDefaultLocation();
                    return;
                }

                if (AppearanceManager.Current.PreferFullscreenMode && ConsiderPreferredFullscreen) {
                    Logging.Debug("Preferred fullscreen, setting default location…");
                    SetDefaultLocation();
                    return;
                }

                try {
                    var oldPlacement = ValuesStorage.Get<string>(LocationAndSizeKey);
                    Placement placement;
                    if (oldPlacement?.Contains(@"WindowPlacementStruct") == true) {
                        placement = new Placement();
                        try {
                            var startIndex = oldPlacement.IndexOf('<');
                            if (startIndex > 0) {
                                oldPlacement = oldPlacement.Remove(0, startIndex);
                            }

                            var d = XDocument.Parse(oldPlacement).Descendants(@"normalPosition").First();
                            placement.Left = d.Elements(@"Left").First().Value.As<int>();
                            placement.Top = d.Elements(@"Top").First().Value.As<int>();
                            placement.Width = d.Elements(@"Right").First().Value.As<int>() - placement.Left;
                            placement.Height = d.Elements(@"Bottom").First().Value.As<int>() - placement.Height;
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    } else {
                        placement = oldPlacement.As<Placement>();
                    }

                    if (placement == null || placement.Width <= 0 || placement.Height <= 0) {
                        Logging.Debug($"No loaded LS to use: {placement?.ToString() ?? @"nothing"}");
                        SetDefaultLocation();
                    } else {
                        Logging.Debug($"Loaded LS: {placement}");

                        WindowState = WindowState.Normal;
                        var savedScreen = Screen.FromPoint(new System.Drawing.Point(placement.Left + 10, placement.Top + 10));
                        var forcedScreen = GetForcedScreen(this);
                        Logging.Debug($"Saved screen: {savedScreen.WorkingArea}, forced screen: {forcedScreen?.WorkingArea.ToString() ?? @"none"}…");

                        if (forcedScreen != null && savedScreen.WorkingArea != forcedScreen.WorkingArea) {
                            Logging.Debug("Saved screen doesn’t match forced one, switching to forced by setting default location…");
                            UpdateLimitations(forcedScreen, Math.Max(placement.ScaleX, 0.2), Math.Max(placement.ScaleY, 0.2));
                            SetDefaultLocation();
                        } else {
                            Logging.Debug("Saved is the same as forced, loading location and size…");
                            UpdateLimitations(savedScreen, Math.Max(placement.ScaleX, 0.2), Math.Max(placement.ScaleY, 0.2));

                            Left = placement.Left;
                            Top = placement.Top;
                            Logging.Debug($"Left and top values are set: {Left}, {Top}");

                            if (MinWidth > savedScreen.WorkingArea.Width) {
                                Logging.Debug($"Clamp min width to: {savedScreen.WorkingArea.Width}");
                                SaveOriginalLimitations();
                                MinWidth = savedScreen.WorkingArea.Width;
                            }

                            if (MinHeight > savedScreen.WorkingArea.Height) {
                                Logging.Debug($"Clamp min height to: {savedScreen.WorkingArea.Height}");
                                SaveOriginalLimitations();
                                MinHeight = savedScreen.WorkingArea.Height;
                            }

                            Width = Math.Min(placement.Width, savedScreen.WorkingArea.Width);
                            Height = Math.Min(placement.Height, savedScreen.WorkingArea.Height);
                            Logging.Debug($"Width and height values are set: {Width}, {Height}");

                            EnsurePositionOnScreen(savedScreen);
                            Logging.Debug("At this point, window should be on screen. Saving size as reference for scaling later…");

                            _windowSize.Width = placement.Width / Math.Max(placement.ScaleX, 0.2);
                            _windowSize.Height = placement.Height / Math.Max(placement.ScaleY, 0.2);
                            Logging.Debug($"Reference size: {_windowSize.Width}×{_windowSize.Height}");
                        }

                        if (placement.IsMaximized) {
                            Logging.Debug($"Window should be maximized, can do now: {_shownAndReady}");

                            if (_shownAndReady) {
                                WindowState = WindowState.Maximized;
                                Logging.Debug("Maximized");
                            } else {
                                _maximizeLater = true;
                                Logging.Debug("Maximize later");
                            }
                        }
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }, 500);
        }

        private bool _shownAndReady, _maximizeLater;

        private void FixFullscreen() {
            Logging.Debug($"Should maximize: {_maximizeLater}");
            if (_maximizeLater) {
                WindowState = WindowState.Maximized;
                Logging.Debug("Maximized");
            }
            _shownAndReady = true;
            Logging.Debug("Window is shown and ready");
        }

        public static readonly DependencyPropertyKey PreferredFullscreenModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreferredFullscreenMode), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty PreferredFullscreenModeProperty = PreferredFullscreenModePropertyKey.DependencyProperty;

        public bool PreferredFullscreenMode => (bool)GetValue(PreferredFullscreenModeProperty);

        protected virtual void OnPreferredFullscreenSet(bool maximizeRequested, bool maximizeDelivered){
            SetValue(PreferredFullscreenModePropertyKey, maximizeDelivered);
        }

        public void UpdatePreferredFullscreenMode(Screen screen, bool maximized = false, bool centerAlign = false) {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            if (maximized || AppearanceManager.Current.PreferFullscreenMode) {
                Logging.Debug("Time to maximize window");

                if (MaxWidth < UnlimitedSize || MaxHeight < UnlimitedSize) {
                    Logging.Debug("Can’t be maximized, align in the middle instead…");
                    WindowState = WindowState.Normal;
                    Width = Math.Min(MaxWidth, screen.WorkingArea.Width);
                    Height = Math.Min(MaxHeight, screen.WorkingArea.Height);
                    Logging.Debug($"Size set: {Width}×{Height}");
                    CenterOnScreen(screen);
                    OnPreferredFullscreenSet(true, false);
                    _maximizeLater = false;
                } else {
                    Top = screen.WorkingArea.Top;
                    Left = screen.WorkingArea.Left;
                    Width = screen.WorkingArea.Width;
                    Height = screen.WorkingArea.Height;
                    Logging.Debug("Dimensions set, maximizing…");

                    if (_shownAndReady) {
                        WindowState = WindowState.Maximized;
                        Logging.Debug("Maximized");
                    } else {
                        _maximizeLater = true;
                        Logging.Debug("Maximize later");
                    }

                    OnPreferredFullscreenSet(true, true);
                }
            } else {
                Logging.Debug("Normal state");

                WindowState = WindowState.Normal;
                if (centerAlign) {
                    CenterOnScreen(screen);
                }

                OnPreferredFullscreenSet(false, false);
                _maximizeLater = false;
            }
        }

        protected virtual void CenterOnScreen(Screen screen) {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - ActualHeight) / 2;
            Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - ActualWidth) / 2;
            Logging.Debug($"Aligned in the middle: {Top}, {Left}");
        }

        private const string DefaultScreenKey = @"DefaultScreen";

        private void SaveDefaultScreen() {
            Logging.Here();

            if (WindowState == WindowState.Minimized) {
                Logging.Debug("Can’t save screen of minimized window");
                return;
            }

            if (!_locationAndSizeInitialized) {
                Logging.Debug("LS isn’t inialized yet");
                return;
            }

            var screen = this.GetScreen();
            var saveAs = new System.Drawing.Point(screen.WorkingArea.Left + 10, screen.WorkingArea.Top + 10);
            Logging.Debug($"Current screen: {screen.WorkingArea}, save as: {saveAs}");
            ValuesStorage.Set(DefaultScreenKey, saveAs);
        }

        private static T LogResult<T>(DpiAwareWindow target, T t, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (OptionVerboseMode) {
                var g = (object)t is Screen s ? s.WorkingArea.ToString() : t.ToString();
                if (target != null) {
                    target.Logging.Debug($"Result: {g}", m, p, l);
                } else {
                    Helpers.Logging.Debug($"Result: {g}", m, p, l);
                }
            }

            return t;
        }

        [NotNull]
        private static Screen GetLastUsedScreen(DpiAwareWindow screenFor = null) {
            var window = screenFor?.Owner ?? LastActiveWindow;
            return LogResult(screenFor, !ReferenceEquals(window, screenFor) && window?.IsLoaded == true ? window.GetScreen()
                    : Screen.FromPoint(ValuesStorage.Get<System.Drawing.Point?>(DefaultScreenKey) ?? Control.MousePosition));
        }

        [NotNull]
        public static Screen GetPreferredScreen(DpiAwareWindow screenFor = null) {
            return LogResult(screenFor, GetForcedScreen(screenFor) ?? GetLastUsedScreen(screenFor));
        }

        [CanBeNull]
        public static Screen GetForcedScreen(DpiAwareWindow screenFor = null) {
            var screenName = AppearanceManager.Current.ForceScreenName;
            var forcedScreen = Screen.AllScreens.FirstOrDefault(x => x.DeviceName == screenName);
            if (forcedScreen != null) {
                if (OptionVerboseMode) {
                    Helpers.Logging.Warning($"Forced: {screenName}, screens: {string.Join("\n", Screen.AllScreens.Select(x => x.WorkingArea))}");
                }
                return LogResult(screenFor, forcedScreen);
            }

            return LogResult(screenFor, AppearanceManager.Current.KeepWithinSingleScreen ? GetLastUsedScreen(screenFor) : null);
        }

        private readonly Busy _locationAndSizeBusy = new Busy();

        private void SaveLocationAndSize() {
            var key = LocationAndSizeKey;
            if (key == null || WindowState == WindowState.Minimized || !_locationAndSizeInitialized
                    || AppearanceManager.Current.PreferFullscreenMode) return;

            _locationAndSizeBusy.DoDelay(() => {
                Logging.Debug("Saving location and size");

                try {
                    ValuesStorage.Set(LocationAndSizeKey, new Placement {
                        Left = (int)Left,
                        Top = (int)Top,
                        Width = (int)ActualWidth,
                        Height = (int)ActualHeight,
                        IsMaximized = WindowState == WindowState.Maximized,
                        ScaleX = _dpi?.ScaleX ?? 1d,
                        ScaleY = _dpi?.ScaleY ?? 1d,
                    });
                    Logging.Debug("Saved");
                    SaveDefaultScreen();
                } catch (Exception e) {
                    Logging.Debug(e);
                }
            }, 300);
        }

        public void EnsurePositionOnScreen(Screen screen) {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            Logging.Debug($"Align within: {screen.WorkingArea}");

            if (Left < screen.WorkingArea.Left) {
                Left = screen.WorkingArea.Left;
                Logging.Debug($"Adjusted left, →: {Left}");
            } else if (Left + ActualWidth > screen.WorkingArea.Right) {
                Left = screen.WorkingArea.Right - ActualWidth;
                Logging.Debug($"Adjusted left, ←: {Left}");
            }

            if (Top < screen.WorkingArea.Top) {
                Top = screen.WorkingArea.Top;
                Logging.Debug($"Adjusted top, ↓: {Top}");
            } else if (Top + ActualHeight > screen.WorkingArea.Bottom) {
                Top = screen.WorkingArea.Bottom - ActualHeight;
                Logging.Debug($"Adjusted top, ↑: {Top}");
            }
        }

        public void EnsureOnScreen(Screen screen) {
            if (!AppearanceManager.Current.ManageWindowsLocation) {
                Logging.Debug("Location management is disabled");
                return;
            }

            Logging.Debug(screen.WorkingArea);

            // Do not change DPI-related original values, so, if window will be moved
            // somewhere else, to a window with different DPI, original values will be
            // restored
            if (MinWidth > screen.WorkingArea.Width) {
                SaveOriginalLimitations();
                Logging.Debug($"Clamp min width to: {screen.WorkingArea.Width}");
                MinWidth = screen.WorkingArea.Width;
            }

            if (MinHeight > screen.WorkingArea.Height) {
                SaveOriginalLimitations();
                Logging.Debug($"Clamp min width to: {screen.WorkingArea.Height}");
                MinHeight = screen.WorkingArea.Height;
            }

            if (ActualWidth > screen.WorkingArea.Width) {
                Logging.Debug($"Clamp width to: {screen.WorkingArea.Width}");
                Width = screen.WorkingArea.Width;
            }

            if (ActualHeight > screen.WorkingArea.Height) {
                Logging.Debug($"Clamp height to: {screen.WorkingArea.Height}");
                Height = screen.WorkingArea.Height;
            }

            EnsurePositionOnScreen(screen);
        }

        public void EnsureOnScreen() {
            Logging.Here();
            EnsureOnScreen(this.GetScreen());
        }
    }
}