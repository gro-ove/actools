using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Data;

namespace AcManager.Controls.Helpers {
    public class FeatureIsAvailable {
        public static string GetFeature(DependencyObject obj) {
            return (string)obj.GetValue(FeatureProperty);
        }

        public static void SetFeature(DependencyObject obj, string value) {
            obj.SetValue(FeatureProperty, value);
        }

        public static readonly DependencyProperty FeatureProperty = DependencyProperty.RegisterAttached("Feature", typeof(string),
                typeof(FeatureIsAvailable), new UIPropertyMetadata(OnFeatureChanged));

        private static void OnFeatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(e.NewValue is string id) || PatchHelper.IsFeatureSupported(id)) return;
            switch (d) {
                case ColumnDefinition element: {
                    element.Width = new GridLength(0d);
                    element.MinWidth = 0d;
                    break;
                }
                case FrameworkElement element:
                    element.Visibility = Visibility.Collapsed;
                    break;
            }
        }
    }
}