using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Presentation {
    public partial class AppearanceManager {
        private readonly StoredValue<bool> _keepWithinSingleScreen = Stored.Get("/Appearance.KeepWithinSingleScreen", true);

        public bool KeepWithinSingleScreen {
            get => _keepWithinSingleScreen.Value;
            set {
                if (Equals(value, _keepWithinSingleScreen.Value)) return;
                _keepWithinSingleScreen.Value = value;
                OnPropertyChanged();
            }
        }

        private readonly StoredValue<bool> _preferFullscreenMode = Stored.Get("/Appearance.PreferFullscreenMode", false);

        public bool PreferFullscreenMode {
            get => _preferFullscreenMode.Value;
            set {
                if (Equals(value, _preferFullscreenMode.Value)) return;
                _preferFullscreenMode.Value = value;
                OnPropertyChanged();

                foreach (var window in Application.Current.Windows.OfType<DpiAwareWindow>().Where(x => x.ConsiderPreferredFullscreen)) {
                    window.UpdatePreferredFullscreenMode(window.GetScreen());
                }
            }
        }
    }
}