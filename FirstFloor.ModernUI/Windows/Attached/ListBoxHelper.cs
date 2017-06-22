using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class ListBoxHelper {
        public static bool GetProperMultiSelectionMode(DependencyObject obj) {
            return (bool)obj.GetValue(ProperMultiSelectionModeProperty);
        }

        public static void SetProperMultiSelectionMode(DependencyObject obj, bool value) {
            obj.SetValue(ProperMultiSelectionModeProperty, value);
        }

        public static readonly DependencyProperty ProperMultiSelectionModeProperty = DependencyProperty.RegisterAttached("ProperMultiSelectionMode", typeof(bool),
                typeof(ListBoxHelper), new UIPropertyMetadata(OnProperMultiSelectionModeChanged));

        private static void OnProperMultiSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as ListBox;
            if (element == null || !(e.NewValue is bool)) return;

            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.SelectionMode = SelectionMode.Extended;
                element.PreviewMouseDown += OnListBoxMouseDown;
                element.MouseUp += OnListBoxMouseUp;
                element.MouseLeave += OnListBoxMouseUp;
                element.PreviewKeyDown += OnListBoxKeyDown;
            } else {
                element.SelectionMode = SelectionMode.Single;
                element.PreviewMouseDown -= OnListBoxMouseDown;
                element.MouseUp -= OnListBoxMouseUp;
                element.MouseLeave -= OnListBoxMouseUp;
                element.PreviewKeyDown -= OnListBoxKeyDown;
            }
        }

        private static void OnListBoxMouseDown(object sender, MouseButtonEventArgs e) {
            var listBox = (ListBox)sender;
            listBox.SelectionMode = Keyboard.Modifiers == ModifierKeys.None ? SelectionMode.Multiple : SelectionMode.Extended;
        }

        private static async void OnListBoxMouseUp(object sender, EventArgs e) {
            var listBox = (ListBox)sender;
            await Task.Delay(1);
            listBox.SelectionMode = SelectionMode.Extended;
        }

        private static void OnListBoxKeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;

            switch (e.Key) {
                case Key.A:
                    ((ListBox)sender).SelectAll();
                    break;
                case Key.D:
                    ((ListBox)sender).SelectedItems.Clear();
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }
    }
}