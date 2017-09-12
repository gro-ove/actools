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
            return obj.GetValue(RegisterProperty) as bool? == true;
        }

        public static void SetRegister(DependencyObject obj, bool value) {
            obj.SetValue(RegisterProperty, value);
        }

        public static readonly DependencyProperty RegisterProperty = DependencyProperty.RegisterAttached("Register", typeof(bool),
                typeof(PopupHelper), new UIPropertyMetadata(OnRegisterChanged));

        private static void OnRegisterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is Popup popup) || !(e.NewValue is bool)) return;
            var newValue = (bool)e.NewValue;
            if (newValue) {
                popup.Opened += PopupOpened;
            } else {
                popup.Opened -= PopupOpened;
            }
        }

        public static int GetGroup(DependencyObject obj) {
            return obj.GetValue(GroupProperty) as int? ?? 0;
        }

        public static void SetGroup(DependencyObject obj, int value) {
            obj.SetValue(GroupProperty, value);
        }

        public static readonly DependencyProperty GroupProperty = DependencyProperty.RegisterAttached("Group", typeof(int),
                typeof(PopupHelper), new UIPropertyMetadata(0));

        public static IEnumerable<Popup> GetOpenPopups() {
            return PresentationSource.CurrentSources.OfType<HwndSource>()
                                     .Select(h => h.RootVisual)
                                     .OfType<FrameworkElement>()
                                     .Select(f => f.Parent)
                                     .OfType<Popup>()
                                     .Where(p => p.IsOpen);
        }

        public static void CloseOpened() {
            foreach (var openPopup in GetOpenPopups().Where(x => !x.StaysOpen)) {
                openPopup.IsOpen = false;
            }
        }

        private static void WindowKeyDown(object sender, RoutedEventArgs e) {
            if (Keyboard.IsKeyDown(Key.Escape)) {
                CloseOpened();
            }
        }

        private static void PopupOpened(object sender, EventArgs eventArgs) {
            if (!(sender is Popup popup)) return;
            var group = GetGroup(popup);
            foreach (var openPopup in GetOpenPopups().Where(x => !ReferenceEquals(x, popup) && (group == 0 || (group & GetGroup(x)) != 0))) {
                openPopup.IsOpen = false;
            }
        }
    }
}