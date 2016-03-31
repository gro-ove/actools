using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Controls {
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

            if ((bool) e.NewValue) {
                element.KeyDown += Element_KeyDown;
            } else {
                element.KeyDown -= Element_KeyDown;
            }
        }

        static void Element_KeyDown(object sender, KeyEventArgs e) {
            if (!e.Key.Equals(Key.Enter)) return;

            var element = sender as UIElement;
            if (element == null) return;

            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }
}
