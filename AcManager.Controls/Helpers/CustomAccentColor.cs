using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Presentation;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.Helpers {
    public static class CustomAccentColor {
        public static void SetCustomAccentColor(this FrameworkElement page, Color color) {
            page.Loaded += (sender, args) => {
                AppearanceManager.Instance.SetAccentColorAsync(color);
            };

            page.Unloaded += (sender, args) => {
                AppearanceManager.Instance.SetAccentColorAsync(AppAppearanceManager.Instance.AccentColor);
            };
        }
    }
}