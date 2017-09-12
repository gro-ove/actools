using System.Windows;

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
    }
}