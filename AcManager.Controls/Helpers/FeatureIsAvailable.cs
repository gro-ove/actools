using System.Windows;
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
            if (d is FrameworkElement element && e.NewValue is string id) {
                element.Visibility = string.IsNullOrWhiteSpace(id) || PatchHelper.IsFeatureSupported(id) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}