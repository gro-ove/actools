using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

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

        private static void OnAdvancesByEnterKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as UIElement;
            if (element == null) return;

            if ((bool)e.NewValue) {
                element.PreviewKeyDown += Element_KeyDown;
            } else {
                element.PreviewKeyDown -= Element_KeyDown;
            }
        }

        public static bool MoveFocus([CanBeNull] DependencyObject element, FocusNavigationDirection direction = FocusNavigationDirection.Next) {
            var e = element as UIElement;
            if (e == null) return false;

            e.MoveFocus(new TraversalRequest(direction));
            return true;
        }

        public static bool RemoveFocus([CanBeNull] DependencyObject element) {
            var parent = element?.GetParents().OfType<IInputElement>().FirstOrDefault(x => x.Focusable);
            if (parent == null) return MoveFocus(element);

            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), parent);
            return true;
        }

        private static void Element_KeyDown(object sender, KeyEventArgs e) {
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
            }
        }
    }
}
