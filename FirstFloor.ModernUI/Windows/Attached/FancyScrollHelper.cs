using System.Windows;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class FancyScrollHelper {
        public static bool GetInvertLocation(DependencyObject obj) {
            return (bool)obj.GetValue(InvertLocationProperty);
        }

        public static void SetInvertLocation(DependencyObject obj, bool value) {
            obj.SetValue(InvertLocationProperty, value);
        }

        public static readonly DependencyProperty InvertLocationProperty = DependencyProperty.RegisterAttached("InvertLocation", typeof(bool),
                typeof(FancyScrollHelper), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetOutsize(DependencyObject obj) {
            return (bool)obj.GetValue(OutsizeProperty);
        }

        public static void SetOutsize(DependencyObject obj, bool value) {
            obj.SetValue(OutsizeProperty, value);
        }

        public static readonly DependencyProperty OutsizeProperty = DependencyProperty.RegisterAttached("Outsize", typeof(bool),
                typeof(FancyScrollHelper), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));


        public static bool GetIsMouseOver(DependencyObject obj) {
            return (bool)obj.GetValue(IsMouseOverProperty);
        }

        public static void SetIsMouseOver(DependencyObject obj, bool value) {
            obj.SetValue(IsMouseOverProperty, value);
        }

        public static readonly DependencyProperty IsMouseOverProperty = DependencyProperty.RegisterAttached("IsMouseOver", typeof(bool),
                typeof(FancyScrollHelper), new UIPropertyMetadata(false));
    }
}