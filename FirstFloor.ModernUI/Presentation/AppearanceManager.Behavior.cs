using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public partial class AppearanceManager {
        private readonly StoredValue<bool> _manageWindowsLocation = Stored.Get("/Appearance.ManageWindowsLocation", true);

        public bool ManageWindowsLocation {
            get => _manageWindowsLocation.Value;
            set => Apply(value, _manageWindowsLocation);
        }

        private readonly StoredValue<bool> _keepWithinSingleScreen = Stored.Get("/Appearance.KeepWithinSingleScreen", true);

        public bool KeepWithinSingleScreen {
            get => _keepWithinSingleScreen.Value;
            set => Apply(value, _keepWithinSingleScreen);
        }

        private readonly StoredValue<string> _forceScreenName = Stored.Get<string>("/Appearance.ForceScreenName");

        [CanBeNull]
        public string ForceScreenName {
            get => _forceScreenName.Value;
            set => Apply(value, _forceScreenName);
        }

        private readonly StoredValue<bool> _preferFullscreenMode = Stored.Get("/Appearance.PreferFullscreenMode", false);

        public bool PreferFullscreenMode {
            get => _preferFullscreenMode.Value;
            set => Apply(value, _preferFullscreenMode, () => {
                foreach (var window in Application.Current.Windows.OfType<DpiAwareWindow>().Where(x => x.ConsiderPreferredFullscreen)) {
                    window.UpdatePreferredFullscreenMode(window.GetScreen());
                }
            });
        }

        private readonly StoredValue<bool> _fullscreenOverTaskbarMode = Stored.Get("/Appearance.FullscreenOverTaskbarMode", false);

        public bool FullscreenOverTaskbarMode {
            get => _fullscreenOverTaskbarMode.Value;
            set {
                if (Equals(value, _fullscreenOverTaskbarMode.Value)) return;

                var toFix = value ? Application.Current.Windows.OfType<DpiAwareWindow>().Where(
                        x => x.WindowState == WindowState.Maximized && x.WindowStyle == WindowStyle.SingleBorderWindow).ToList() : null;

                _fullscreenOverTaskbarMode.Value = value;
                OnPropertyChanged();

                if (toFix != null) {
                    foreach (var window in toFix) {
                        window.WindowState = WindowState.Normal;
                        window.WindowState = WindowState.Maximized;
                    }
                }
            }
        }
    }
}