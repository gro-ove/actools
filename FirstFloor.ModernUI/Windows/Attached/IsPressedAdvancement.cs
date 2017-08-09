using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class IsPressedAdvancement {
        public static readonly DependencyProperty IsPressedProperty = DependencyProperty.RegisterAttached("IsPressed", typeof(bool),
                typeof(IsPressedAdvancement), new PropertyMetadata(false));

        public static readonly DependencyProperty AttachIsPressedProperty = DependencyProperty.RegisterAttached("AttachIsPressed", typeof(bool),
                typeof(IsPressedAdvancement), new PropertyMetadata(false, PropertyChangedCallback));

        public static void PropertyChangedCallback(DependencyObject depObj, DependencyPropertyChangedEventArgs args) {
            var element = (FrameworkElement)depObj;
            if (element == null) return;

            if ((bool)args.NewValue) {
                element.MouseDown += OnMouseDown;
                element.MouseUp += OnMouseUp;
                element.MouseLeave += OnMouseLeave;
            } else {
                element.MouseDown -= OnMouseDown;
                element.MouseUp -= OnMouseUp;
                element.MouseLeave -= OnMouseLeave;
            }
        }

        static void OnMouseLeave(object sender, MouseEventArgs e) {
            var element = (FrameworkElement)sender;
            element?.SetValue(IsPressedProperty, false);
        }

        static void OnMouseUp(object sender, MouseButtonEventArgs e) {
            var element = (FrameworkElement)sender;
            element?.SetValue(IsPressedProperty, false);
        }

        static void OnMouseDown(object sender, MouseButtonEventArgs e) {
            var element = (FrameworkElement)sender;
            element?.SetValue(IsPressedProperty, true);
        }

        public static bool GetIsPressed(UIElement element) {
            return (bool)element.GetValue(IsPressedProperty);
        }

        public static void SetIsPressed(UIElement element, bool val) {
            element.SetValue(IsPressedProperty, val);
        }

        public static bool GetAttachIsPressed(UIElement element) {
            return (bool)element.GetValue(AttachIsPressedProperty);
        }

        public static void SetAttachIsPressed(UIElement element, bool val) {
            element.SetValue(AttachIsPressedProperty, val);
        }
    }
}