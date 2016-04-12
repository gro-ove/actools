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
        
        private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var control = d as UIElement;
            if (control == null) return;
            if (e.OldValue == null) {
                control.MouseLeftButtonDown += Control_MouseDown;
            } else if (e.NewValue == null) {
                control.MouseLeftButtonDown -= Control_MouseDown;
            }

            // control.Cursor = e.NewValue == null ? Cursors.Arrow : Cursors.SizeAll;
        }

        private static void Control_MouseDown(object sender, MouseEventArgs e) {
            new ImageViewer(GetImage((DependencyObject)sender)).ShowDialog();
        }
    }
}
