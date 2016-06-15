using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class FocusAdvancement {
        public static bool GetAdvancesByEnterKey(DependencyObject obj) {
            return (bool)obj.GetValue(AdvancesByEnterKeyProperty);
        }

        public static void SetAdvancesByEnterKey(DependencyObject obj, bool value) {
            obj.SetValue(AdvancesByEnterKeyProperty, value);
        }

        public static readonly DependencyProperty AdvancesByEnterKeyProperty = DependencyProperty.RegisterAttached("AdvancesByEnterKey",
            typeof(bool), typeof(FocusAdvancement), new UIPropertyMetadata(OnAdvancesByEnterKeyPropertyChanged));

        static void OnAdvancesByEnterKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as UIElement;
            if (element == null) return;

            if ((bool)e.NewValue) {
                element.KeyDown += Element_KeyDown;
            } else {
                element.KeyDown -= Element_KeyDown;
            }
        }

        static void Element_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape: {
                    var element = sender as DependencyObject;
                    var parent = element?.GetParents().OfType<IInputElement>().FirstOrDefault(x => x.Focusable);
                    if (parent != null) {
                        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), parent);
                        break;
                    }

                    goto case Key.Enter;
                }

                case Key.Enter: {
                    var element = sender as UIElement;
                    if (element == null) return;

                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                    break;
                }
            }
        }
    }
}
