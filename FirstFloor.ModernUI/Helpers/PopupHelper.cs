using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class PopupHelper {
        public static void Initialize() {
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new RoutedEventHandler(WindowKeyDown));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseLeftButtonDownEvent, new RoutedEventHandler(WindowMouseClick));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseRightButtonDownEvent, new RoutedEventHandler(WindowMouseClick));
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

            popup.Opened -= PopupOpened;
            popup.Closed -= PopupClosed;

            if (newValue) {
                popup.Opened += PopupOpened;
                popup.Closed += PopupClosed;
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

        private static IEnumerable<Popup> GetOpenPopups() {
            return PresentationSource.CurrentSources.OfType<HwndSource>()
                                     .Select(h => h.RootVisual)
                                     .OfType<FrameworkElement>()
                                     .Select(f => f.Parent)
                                     .OfType<Popup>()
                                     .Where(p => p.IsOpen);
        }

        private static void CloseOpened() {
            foreach (var openPopup in GetOpenPopups().Where(x => !x.StaysOpen)) {
                openPopup.IsOpen = false;
            }
        }

        private static void WindowKeyDown(object sender, RoutedEventArgs e) {
            if (Keyboard.IsKeyDown(Key.Escape)) {
                CloseOpened();
            }
        }

        private static bool HasPopupParent([CanBeNull] DependencyObject obj) {
            for (; obj != null; obj = obj.GetParent()) {
                if (obj is Popup) return true;
            }
            return false;
        }

        private static void WindowMouseClick(object sender, RoutedEventArgs e) {
            var list = Opened;
            if (list.Count == 0 || HasPopupParent(e.OriginalSource as DependencyObject)) return;
            for (var i = list.Count - 1; i >= 0; i--) {
                var popup = list[i];
                if (!popup.StaysOpen) {
                    popup.IsOpen = false;
                    list.RemoveAt(i);
                }
            }
        }

        private static readonly List<Popup> Opened = new List<Popup>();

        private static void PopupOpened(object sender, EventArgs eventArgs) {
            if (!(sender is Popup popup)) return;

            if (!popup.StaysOpen) {
                Opened.Add(popup);
            }

            var group = GetGroup(popup);
            foreach (var openPopup in GetOpenPopups().Where(x => !ReferenceEquals(x, popup) && (group == 0 || (group & GetGroup(x)) != 0))) {
                openPopup.IsOpen = false;
            }
        }

        private static void PopupClosed(object sender, EventArgs e) {
            if (!(sender is Popup popup)) return;
            Opened.Remove(popup);
        }
    }
}