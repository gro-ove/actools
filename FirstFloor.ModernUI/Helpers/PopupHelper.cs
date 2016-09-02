using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace FirstFloor.ModernUI.Helpers {
    public class PopupHelper {
        public static void Initialize() {
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new RoutedEventHandler(WindowKeyDown));
        }

        public static bool GetRegister(DependencyObject obj) {
            return (bool)obj.GetValue(RegisterProperty);
        }

        public static void SetRegister(DependencyObject obj, bool value) {
            obj.SetValue(RegisterProperty, value);
        }

        public static readonly DependencyProperty RegisterProperty = DependencyProperty.RegisterAttached("Register", typeof(bool),
                typeof(PopupHelper), new UIPropertyMetadata(OnRegisterChanged));

        private static void OnRegisterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var popup = d as Popup;
            if (popup == null || !(e.NewValue is bool)) return;

            var newValue = (bool)e.NewValue;
            if (newValue) {
                popup.Opened += PopupOpened;
            } else {
                popup.Opened -= PopupOpened;
            }
        }

        public static IEnumerable<Popup> GetOpenPopups() {
            return PresentationSource.CurrentSources.OfType<HwndSource>()
                                     .Select(h => h.RootVisual)
                                     .OfType<FrameworkElement>()
                                     .Select(f => f.Parent)
                                     .OfType<Popup>()
                                     .Where(p => p.IsOpen);
        }

        public static void CloseOpened() {
            foreach (var openPopup in GetOpenPopups()) {
                openPopup.IsOpen = false;
            }
        }

        private static void WindowKeyDown(object sender, RoutedEventArgs e) {
            if (Keyboard.IsKeyDown(Key.Escape)) {
                CloseOpened();
            }
        }

        private static void PopupOpened(object sender, EventArgs eventArgs) {
            var popup = sender as Popup;
            if (popup == null) return;

            foreach (var openPopup in GetOpenPopups().Where(x => !ReferenceEquals(x, popup))) {
                openPopup.IsOpen = false;
            }
        }
    }
}