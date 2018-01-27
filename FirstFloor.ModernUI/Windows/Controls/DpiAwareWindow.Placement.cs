using System;
using System.IO;
using System.Linq;
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
                typeof(DpiAwareWindow), new PropertyMetadata(null, (o, e) => { ((DpiAwareWindow)o)._locationAndSizeKey = (string)e.NewValue; }));

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
        }

        private void SetDefaultLocation() {
            var screen = GetPreferredScreen(this);
            var originalState = WindowState;
            if (originalState != WindowState.Normal) {
                WindowState = WindowState.Normal;
            }

            if (ActualWidth > screen.Bounds.Width) {
                // Do not change DPI-related original values, so, if window will be moved
                // somewhere else, to a window with different DPI, original values will be
                // restored
                if (MinWidth > screen.Bounds.Width) {
                    SaveOriginalLimitations();
                    MinWidth = screen.Bounds.Width;
                }

                Width = screen.Bounds.Width;
            }

            if (ActualHeight > screen.Bounds.Height) {
                if (MinHeight > screen.Bounds.Height) {
                    SaveOriginalLimitations();
                    MinHeight = screen.Bounds.Height;
                }

                Height = screen.Bounds.Height;
            }

            UpdatePreferredFullscreenMode(screen, originalState != WindowState.Normal, true);
        }

        protected virtual void OnInitializedOverride() { }

        protected sealed override void OnInitialized(EventArgs e) {
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
            _locationAndSizeInitialized = true;
            _locationAndSizeBusy.DoDelayAfterwards(() => {
                if (LocationAndSizeKey == null) {
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

                    if (AppearanceManager.Current.PreferFullscreenMode && ConsiderPreferredFullscreen
                            || placement == null || placement.Width <= 0 || placement.Height <= 0) {
                        SetDefaultLocation();
                    } else {
                        UpdateLimitations(Math.Max(placement.ScaleX, 0.2), Math.Max(placement.ScaleY, 0.2));

                        WindowState = WindowState.Normal;
                        var savedScreen = Screen.FromPoint(new System.Drawing.Point(
                                placement.Left + placement.Width / 2, placement.Top + placement.Height / 2));
                        var activeScreen = AppearanceManager.Current.KeepWithinSingleScreen ? GetPreferredScreen(this) : null;
                        if (activeScreen != null && savedScreen.Bounds != activeScreen.Bounds) {
                            SetDefaultLocation();
                        } else {
                            Left = placement.Left;
                            Top = placement.Top;

                            if (MinWidth > savedScreen.Bounds.Width) {
                                MinWidth = savedScreen.Bounds.Width;
                            }

                            if (MinHeight > savedScreen.Bounds.Height) {
                                MinHeight = savedScreen.Bounds.Height;
                            }

                            Width = Math.Min(placement.Width, savedScreen.Bounds.Width);
                            Height = Math.Min(placement.Height, savedScreen.Bounds.Height);
                            EnsurePositionOnScreen(savedScreen);

                            _windowSize.Width = placement.Width / Math.Max(placement.ScaleX, 0.2);
                            _windowSize.Height = placement.Height / Math.Max(placement.ScaleY, 0.2);
                        }

                        if (placement.IsMaximized) {
                            if (_shownAndReady) {
                                WindowState = WindowState.Maximized;
                            } else {
                                _maximizeLater = true;
                            }
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }, 500);
        }

        private bool _shownAndReady, _maximizeLater;

        private void FixFullscreen() {
            if (_maximizeLater) {
                WindowState = WindowState.Maximized;
            }
            _shownAndReady = true;
        }

        public static readonly DependencyPropertyKey PreferredFullscreenModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreferredFullscreenMode), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty PreferredFullscreenModeProperty = PreferredFullscreenModePropertyKey.DependencyProperty;

        public bool PreferredFullscreenMode => (bool)GetValue(PreferredFullscreenModeProperty);

        protected virtual void OnPreferredFullscreenSet(bool maximizeRequested, bool maximizeDelivered){
            SetValue(PreferredFullscreenModePropertyKey, maximizeDelivered);
        }

        public void UpdatePreferredFullscreenMode(Screen screen, bool maximized = false, bool centerAlign = false) {
            if (maximized || AppearanceManager.Current.PreferFullscreenMode) {
                if (MaxWidth < UnlimitedSize || MaxHeight < UnlimitedSize) {
                    WindowState = WindowState.Normal;
                    Width = Math.Min(MaxWidth, screen.Bounds.Width);
                    Height = Math.Min(MaxHeight, screen.Bounds.Height);
                    CenterOnScreen(screen);
                    OnPreferredFullscreenSet(true, false);
                    _maximizeLater = false;
                } else {
                    Top = screen.Bounds.Top;
                    Left = screen.Bounds.Left;
                    Width = screen.Bounds.Width;
                    Height = screen.Bounds.Height;

                    if (_shownAndReady) {
                        WindowState = WindowState.Maximized;
                    } else {
                        _maximizeLater = true;
                    }

                    OnPreferredFullscreenSet(true, true);
                }
            } else {
                WindowState = WindowState.Normal;
                if (centerAlign) {
                    CenterOnScreen(screen);
                }

                OnPreferredFullscreenSet(false, false);
                _maximizeLater = false;
            }
        }

        protected virtual void CenterOnScreen(Screen screen) {
            Top = screen.Bounds.Top + (screen.Bounds.Height - ActualHeight) / 2;
            Left = screen.Bounds.Left + (screen.Bounds.Width - ActualWidth) / 2;
        }

        private const string DefaultScreenKey = @"DefaultScreen";

        private void SaveDefaultScreen() {
            if (WindowState == WindowState.Minimized || !_locationAndSizeInitialized) return;
            var screen = this.GetScreen();
            ValuesStorage.Set(DefaultScreenKey, new System.Drawing.Point(
                    screen.Bounds.Left + screen.Bounds.Width / 2,
                    screen.Bounds.Top + screen.Bounds.Height / 2));
        }

        [CanBeNull]
        public static Screen GetActiveScreen(DpiAwareWindow screenFor = null) {
            var window = screenFor?.Owner ?? LastActiveWindow;
            return ReferenceEquals(window, screenFor) || window?.IsLoaded != true ? null : window.GetScreen();
        }

        [NotNull]
        public static Screen GetPreferredScreen(DpiAwareWindow screenFor = null) {
            return GetActiveScreen(screenFor) ?? Screen.FromPoint(ValuesStorage.Get<System.Drawing.Point?>(DefaultScreenKey) ?? Control.MousePosition);
        }

        private readonly Busy _locationAndSizeBusy = new Busy();

        private void SaveLocationAndSize() {
            var key = LocationAndSizeKey;
            if (key == null || WindowState == WindowState.Minimized || !_locationAndSizeInitialized
                    || AppearanceManager.Current.PreferFullscreenMode) return;

            _locationAndSizeBusy.DoDelay(() => {
                try {
                    ValuesStorage.Set(LocationAndSizeKey, new Placement {
                        Left = (int)Left,
                        Top = (int)Top,
                        Width = (int)Width,
                        Height = (int)Height,
                        IsMaximized = WindowState == WindowState.Maximized,
                        ScaleX = _dpi?.ScaleX ?? 1d,
                        ScaleY = _dpi?.ScaleY ?? 1d,
                    });
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }, 100);
        }

        public void EnsurePositionOnScreen(Screen screen) {
            if (Left < screen.Bounds.Left) {
                Left = screen.Bounds.Left;
            } else if (Left + ActualWidth > screen.Bounds.Right) {
                Left = screen.Bounds.Right - ActualWidth;
            }

            if (Top < screen.Bounds.Top) {
                Top = screen.Bounds.Top;
            } else if (Top + ActualHeight > screen.Bounds.Bottom) {
                Top = screen.Bounds.Bottom - ActualHeight;
            }
        }

        public void EnsureOnScreen(Screen screen) {
            if (ActualWidth > screen.Bounds.Width) {
                // Do not change DPI-related original values, so, if window will be moved
                // somewhere else, to a window with different DPI, original values will be
                // restored
                if (MinWidth > screen.Bounds.Width) {
                    SaveOriginalLimitations();
                    MinWidth = screen.Bounds.Width;
                }

                Width = screen.Bounds.Width;
            }

            if (ActualHeight > screen.Bounds.Height) {
                if (MinHeight > screen.Bounds.Height) {
                    SaveOriginalLimitations();
                    MinHeight = screen.Bounds.Height;
                }

                Height = screen.Bounds.Height;
            }

            EnsurePositionOnScreen(screen);
        }

        public void EnsureOnScreen() {
            EnsureOnScreen(this.GetScreen());
        }
    }
}