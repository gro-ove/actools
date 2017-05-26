using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class RememberingExpander : Expander {
        public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(nameof(Key), typeof(string),
                typeof(RememberingExpander), new PropertyMetadata(OnKeyChanged));

        public string Key {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        private static void OnKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((RememberingExpander)o).OnKeyChanged((string)e.NewValue);
        }

        private void OnKeyChanged(string newValue) {
            IsExpanded = ValuesStorage.GetBool(newValue, true);
        }

        protected override void OnExpanded() {
            base.OnExpanded();
            if (Key != null) {
                ValuesStorage.Set(Key, true);
            }
        }

        protected override void OnCollapsed() {
            base.OnCollapsed();
            if (Key != null) {
                ValuesStorage.Set(Key, false);
            }
        }
    }
}