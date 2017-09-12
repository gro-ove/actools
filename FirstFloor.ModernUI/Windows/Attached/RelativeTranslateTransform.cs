using System.Windows;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class RelativeTranslateTransform {
        public static double GetX(DependencyObject obj) {
            return obj.GetValue(XProperty) as double? ?? 0d;
        }

        public static void SetX(DependencyObject obj, double value) {
            obj.SetValue(XProperty, value);
        }

        public static readonly DependencyProperty XProperty = DependencyProperty.RegisterAttached("X", typeof(double),
                typeof(RelativeTranslateTransform), new UIPropertyMetadata(0d, OnXChanged));

        private static void OnXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element) || !(e.NewValue is double)) return;
            Update(element);
        }

        public static double GetY(DependencyObject obj) {
            return obj.GetValue(YProperty) as double? ?? 0d;
        }

        public static void SetY(DependencyObject obj, double value) {
            obj.SetValue(YProperty, value);
        }

        public static readonly DependencyProperty YProperty = DependencyProperty.RegisterAttached("Y", typeof(double),
                typeof(RelativeTranslateTransform), new UIPropertyMetadata(0d, OnYChanged));

        private static void OnYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element) || !(e.NewValue is double)) return;
            Update(element);
        }

        public static FrameworkElement GetRelativeTo(DependencyObject obj) {
            return (FrameworkElement)obj.GetValue(RelativeToProperty);
        }

        public static void SetRelativeTo(DependencyObject obj, FrameworkElement value) {
            obj.SetValue(RelativeToProperty, value);
        }

        public static readonly DependencyProperty RelativeToProperty = DependencyProperty.RegisterAttached("RelativeTo", typeof(FrameworkElement),
                typeof(RelativeTranslateTransform), new UIPropertyMetadata(OnRelativeToChanged));

        private static void OnRelativeToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || !(e.NewValue is FrameworkElement)) return;

            var nv = (FrameworkElement)e.NewValue;
            nv.SizeChanged += (sender, args) => {
                Update(element);
            };

            Update(element);
        }

        private static void Update(FrameworkElement element) {
            var relativeTo = GetRelativeTo(element) ?? element;

            var translate = element.RenderTransform as TranslateTransform;
            if (translate == null) {
                translate = new TranslateTransform();
                element.RenderTransform = translate;
            }

            translate.X = GetX(element) * relativeTo.ActualWidth;
            translate.Y = GetY(element) * relativeTo.ActualHeight;
        }
    }
}