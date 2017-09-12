using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernTabSplitter : GridSplitter {
        public ModernTabSplitter() {
            DefaultStyleKey = typeof(ModernTabSplitter);
            DragCompleted += OnDragCompleted;
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);
            SetWidth(LoadWidth());
        }

        private void SetWidth(double width) {
            var column = (Parent as Grid)?.ColumnDefinitions.FirstOrDefault();
            if (column != null) {
                column.Width = new GridLength(width, GridUnitType.Pixel);
            }
        }

        private double? GetWidth() {
            return (Parent as Grid)?.ColumnDefinitions.FirstOrDefault()?.ActualWidth;
        }

        private double LoadWidth() {
            return ValuesStorage.GetDouble(SaveKeyValue, InitialWidth);
        }

        private void SaveWidth(double value) {
            ValuesStorage.Set(SaveKeyValue, value);
        }

        private void OnDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) {
            if (SaveKey != null) {
                var value = GetWidth();
                if (value.HasValue) {
                    SaveWidth(value.Value);
                }
            }
        }

        public static readonly DependencyProperty InitialWidthProperty = DependencyProperty.Register(nameof(InitialWidth), typeof(double),
                typeof(ModernTabSplitter), new PropertyMetadata(200d));

        public double InitialWidth {
            get => GetValue(InitialWidthProperty) as double? ?? 200d;
            set => SetValue(InitialWidthProperty, value);
        }

        private string SaveKeyValue => $"ModernTabSplitter:{SaveKey}";

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernTabSplitter), new PropertyMetadata(@"ModernTab", OnSaveKeyChanged));

        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTabSplitter)o).OnSaveKeyChanged();
        }

        private void OnSaveKeyChanged() {
            SetWidth(LoadWidth());
        }
    }
}