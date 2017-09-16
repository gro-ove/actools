using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;

namespace AcManager.Controls.Services {
    public static class ImageViewerService {
        public static string GetImage(DependencyObject obj) {
            return (string)obj.GetValue(ImageProperty);
        }

        public static void SetImage(DependencyObject obj, string value) {
            obj.SetValue(ImageProperty, value);
        }

        public static readonly DependencyProperty ImageProperty = DependencyProperty.RegisterAttached("Image",
            typeof(string), typeof(ImageViewerService), new FrameworkPropertyMetadata(null, OnImageChanged));

        public static double GetMaxWidth(DependencyObject obj) {
            return obj.GetValue(MaxWidthProperty) as double? ?? 0d;
        }

        public static void SetMaxWidth(DependencyObject obj, double value) {
            obj.SetValue(MaxWidthProperty, value);
        }

        public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.RegisterAttached("MaxWidth",
            typeof(double), typeof(ImageViewerService), new FrameworkPropertyMetadata(double.MaxValue));

        public static double GetMaxHeight(DependencyObject obj) {
            return obj.GetValue(MaxHeightProperty) as double? ?? 0d;
        }

        public static void SetMaxHeight(DependencyObject obj, double value) {
            obj.SetValue(MaxHeightProperty, value);
        }

        public static readonly DependencyProperty MaxHeightProperty = DependencyProperty.RegisterAttached("MaxHeight",
            typeof(double), typeof(ImageViewerService), new FrameworkPropertyMetadata(double.MaxValue));

        private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is UIElement control)) return;
            control.MouseLeftButtonUp -= OnMouseUp;
            if (e.NewValue != null) {
                control.MouseLeftButtonUp += OnMouseUp;
            }
        }

        private static void OnMouseUp(object sender, MouseEventArgs e) {
            if (e.Handled) return;
            var d = (DependencyObject)sender;
            e.Handled = true;
            new ImageViewer(GetImage(d), GetMaxWidth(d), GetMaxHeight(d)).ShowDialog();
        }
    }
}
