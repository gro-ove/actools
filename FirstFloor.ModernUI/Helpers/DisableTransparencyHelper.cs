using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace FirstFloor.ModernUI.Helpers {
    public static class DisableTransparencyHelper {
        private static void DisableWindowsTransparency(DependencyProperty dp, Type type) {
            try {
                var m = dp.GetMetadata(type);
                var p = m.GetType().GetProperty("Sealed",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?? throw new Exception(@"Sealed property is missing");
                p.SetValue(m, false);
                m.CoerceValueCallback = (d, o) => {
                    if ((d as UIElement)?.IsHitTestVisible == false) return o as bool? == true;
                    return false;
                };
                p.SetValue(m, true);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static void Disable() {
            DisableWindowsTransparency(Window.AllowsTransparencyProperty, typeof(Window));
            DisableWindowsTransparency(Popup.AllowsTransparencyProperty, typeof(Popup));
        }
    }
}