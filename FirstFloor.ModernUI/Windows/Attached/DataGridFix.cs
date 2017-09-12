using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class DataGridFix {
        public static bool GetEnabled(DependencyObject obj) {
            return obj.GetValue(EnabledProperty) as bool? == true;
        }

        public static void SetEnabled(DependencyObject obj, bool value) {
            obj.SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool),
                typeof(DataGridFix), new UIPropertyMetadata(OnEnabledChanged));

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is DataGrid element) || !(e.NewValue is bool)) return;
            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.Loaded += OnDataGridLoaded;
                element.Unloaded += OnDataGridLoaded;
            } else {
                element.Loaded -= OnDataGridLoaded;
                element.Unloaded -= OnDataGridLoaded;
            }
        }

        private static void OnDataGridLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var grid = (DataGrid)sender;
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}