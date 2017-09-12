using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    // temporary
    // todo: replace by BetterTextBox
    public static class FocusAdvancement {
        [Obsolete]
        public static bool GetAdvancesByEnterKey(DependencyObject obj) {
            return obj.GetValue(AdvancesByEnterKeyProperty) as bool? == true;
        }

        [Obsolete]
        public static void SetAdvancesByEnterKey(DependencyObject obj, bool value) {
            obj.SetValue(AdvancesByEnterKeyProperty, value);
        }

        [Obsolete]
        public static readonly DependencyProperty AdvancesByEnterKeyProperty = DependencyProperty.RegisterAttached("AdvancesByEnterKey",
            typeof(bool), typeof(FocusAdvancement), new UIPropertyMetadata(OnAdvancesByEnterKeyPropertyChanged));

        private static void OnAdvancesByEnterKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is UIElement element)) return;
            if ((bool)e.NewValue) {
                element.PreviewKeyDown += OnKeyDown;
            } else {
                element.PreviewKeyDown -= OnKeyDown;
            }
        }

        public static bool MoveFocus([CanBeNull] this DependencyObject element, FocusNavigationDirection direction = FocusNavigationDirection.Next) {
            if (!(element is UIElement e)) return false;
            e.MoveFocus(new TraversalRequest(direction));
            return true;
        }

        public static bool RemoveFocus([CanBeNull] this DependencyObject element) {
            var parent = element?.GetParents().OfType<IInputElement>().FirstOrDefault(x => x.Focusable);
            if (parent == null) return MoveFocus(element);

            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), parent);
            return true;
        }

        public static void OnKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    if (RemoveFocus(sender as DependencyObject)) {
                        e.Handled = true;
                    }
                    break;
                case Key.Enter:
                    if (MoveFocus(sender as DependencyObject)) {
                        e.Handled = true;
                    }
                    break;
                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift && MoveFocus(sender as DependencyObject, FocusNavigationDirection.Previous)) {
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}
