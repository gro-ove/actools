using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Pages.Dialogs;

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
            return (double)obj.GetValue(MaxWidthProperty);
        }

        public static void SetMaxWidth(DependencyObject obj, double value) {
            obj.SetValue(MaxWidthProperty, value);
        }

        public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.RegisterAttached("MaxWidth", 
            typeof(double), typeof(ImageViewerService), new FrameworkPropertyMetadata(double.MaxValue));

        public static double GetMaxHeight(DependencyObject obj) {
            return (double)obj.GetValue(MaxHeightProperty);
        }

        public static void SetMaxHeight(DependencyObject obj, double value) {
            obj.SetValue(MaxHeightProperty, value);
        }

        public static readonly DependencyProperty MaxHeightProperty = DependencyProperty.RegisterAttached("MaxHeight", 
            typeof(double), typeof(ImageViewerService), new FrameworkPropertyMetadata(double.MaxValue));
        
        private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = d as UIElement;
            if (control == null) return;
            if (e.OldValue == null) {
                control.MouseLeftButtonDown += Control_MouseDown;
            } else if (e.NewValue == null) {
                control.MouseLeftButtonDown -= Control_MouseDown;
            }
        }

        private static void Control_MouseDown(object sender, MouseEventArgs e) {
            var d = (DependencyObject)sender;
            var w = GetMaxWidth(d);
            var h = GetMaxHeight(d);
            new ImageViewer(new[] { GetImage(d) }, double.IsPositiveInfinity(w) ? -1 : (int)w, double.IsPositiveInfinity(h) ? -1 : (int)h).ShowDialog();
        }
    }
}
