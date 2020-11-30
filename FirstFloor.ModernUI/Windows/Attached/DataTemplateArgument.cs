using System.Windows;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class DataTemplateArgument {
        public static object GetString1(DependencyObject obj) {
            return obj.GetValue(String1Property);
        }

        public static void SetString1(DependencyObject obj, object value) {
            obj.SetValue(String1Property, value);
        }

        public static readonly DependencyProperty String1Property = DependencyProperty.RegisterAttached("String1", typeof(object),
                typeof(DataTemplateArgument), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        public static object GetFlag1(DependencyObject obj) {
            return (bool)obj.GetValue(Flag1Property);
        }

        public static void SetFlag1(DependencyObject obj, bool value) {
            obj.SetValue(Flag1Property, value);
        }

        public static readonly DependencyProperty Flag1Property = DependencyProperty.RegisterAttached("Flag1", typeof(bool),
                typeof(DataTemplateArgument), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    }
}