using System.Windows;
using System.Windows.Interop;
using FirstFloor.ModernUI.Win32;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public static readonly DependencyPropertyKey ActualLeftPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualLeft), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualLeftProperty = ActualLeftPropertyKey.DependencyProperty;
        public double ActualLeft => GetValue(ActualLeftProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualTopPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualTop), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualTopProperty = ActualTopPropertyKey.DependencyProperty;
        public double ActualTop => GetValue(ActualTopProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualRightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualRight), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualRightProperty = ActualRightPropertyKey.DependencyProperty;
        public double ActualRight => GetValue(ActualRightProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualBottomPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualBottom), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualBottomProperty = ActualBottomPropertyKey.DependencyProperty;
        public double ActualBottom => GetValue(ActualBottomProperty) as double? ?? 0d;

        private Win32Rect GetWindowRectangle() {
            NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect);
            return rect;
        }

        private void UpdateActualLocation() {
            if (WindowState == WindowState.Maximized) {
                var rect = GetWindowRectangle();
                SetValue(ActualTopPropertyKey, (double)rect.Top);
                SetValue(ActualLeftPropertyKey, (double)rect.Left);
            } else {
                SetValue(ActualTopPropertyKey, Top);
                SetValue(ActualLeftPropertyKey, Left);
            }

            SetValue(ActualBottomPropertyKey, ActualTop + ActualHeight);
            SetValue(ActualRightPropertyKey, ActualLeft + ActualWidth);
        }
    }
}