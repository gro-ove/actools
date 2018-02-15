using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class FancyScrollHelper {
        public static bool GetInvertLocation(DependencyObject obj) {
            return obj.GetValue(InvertLocationProperty) as bool? == true;
        }

        public static void SetInvertLocation(DependencyObject obj, bool value) {
            obj.SetValue(InvertLocationProperty, value);
        }

        public static readonly DependencyProperty InvertLocationProperty = DependencyProperty.RegisterAttached("InvertLocation", typeof(bool),
                typeof(FancyScrollHelper), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetOutside(DependencyObject obj) {
            return obj.GetValue(OutsideProperty) as bool? == true;
        }

        public static void SetOutside(DependencyObject obj, bool value) {
            obj.SetValue(OutsideProperty, value);
        }

        public static readonly DependencyProperty OutsideProperty = DependencyProperty.RegisterAttached("Outside", typeof(bool),
                typeof(FancyScrollHelper), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));


        public static bool GetIsMouseOver(DependencyObject obj) {
            return obj.GetValue(IsMouseOverProperty) as bool? == true;
        }

        public static void SetIsMouseOver(DependencyObject obj, bool value) {
            obj.SetValue(IsMouseOverProperty, value);
        }

        public static readonly DependencyProperty IsMouseOverProperty = DependencyProperty.RegisterAttached("IsMouseOver", typeof(bool),
                typeof(FancyScrollHelper), new UIPropertyMetadata(false));

        public static bool GetScrollParent(DependencyObject obj) {
            return (bool)obj.GetValue(ScrollParentProperty);
        }

        public static void SetScrollParent(DependencyObject obj, bool value) {
            obj.SetValue(ScrollParentProperty, value);
        }

        public static readonly DependencyProperty ScrollParentProperty = DependencyProperty.RegisterAttached("ScrollParent", typeof(bool),
                typeof(FancyScrollHelper), new UIPropertyMetadata(OnScrollParentChanged));

        private static void OnScrollParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is Panel element && e.NewValue is bool newValue) {
                if (newValue) {
                    element.PreviewMouseMove += OnScrollParentMouse;
                    element.MouseLeave += OnScrollParentMouse;
                } else {
                    element.PreviewMouseMove -= OnScrollParentMouse;
                    element.MouseLeave -= OnScrollParentMouse;
                }
            }
        }

        private const double Threshold = 20;

        private static void OnScrollParentMouse(object sender, MouseEventArgs args) {
            var parent = (Panel)sender;

            ScrollBar scroll = null;
            for (var i = 0; i < parent.Children.Count; i++) {
                var child = parent.Children[i];
                if (child is ScrollBar s) {
                    scroll = s;
                    break;
                }
            }

            if (scroll == null) return;
            var pos = args.GetPosition(parent);
            SetIsMouseOver(scroll, parent.IsMouseOver && (scroll.HorizontalAlignment == HorizontalAlignment.Right && pos.X > parent.ActualWidth - Threshold
                    || scroll.HorizontalAlignment == HorizontalAlignment.Left && pos.X < Threshold
                    || scroll.VerticalAlignment == VerticalAlignment.Bottom && pos.Y > parent.ActualHeight - Threshold
                    || scroll.VerticalAlignment == VerticalAlignment.Top && pos.Y < Threshold));
        }
    }
}