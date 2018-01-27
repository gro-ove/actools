using System;
using System.Windows;
using System.Windows.Interop;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Win32;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public static readonly DependencyProperty PreventActivationProperty = DependencyProperty.Register(nameof(PreventActivation), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(OnPreventActivationChanged));

        public bool PreventActivation {
            get => (bool)GetValue(PreventActivationProperty);
            set => SetValue(PreventActivationProperty, value);
        }

        private static void OnPreventActivationChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DpiAwareWindow)o).OnPreventActivationChanged((bool)e.NewValue);
        }

        private void OnPreventActivationChanged(bool newValue) {
            try {
                if (!IsLoaded) return;
                var handle = new WindowInteropHelper(this).Handle;
                var current = NativeMethods.GetWindowExStyle(handle);
                NativeMethods.SetWindowExStyle(handle,
                        newValue ? current | WindowExStyle.NoActivate : current & ~WindowExStyle.NoActivate);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static readonly DependencyProperty ToolWindowProperty = DependencyProperty.Register(nameof(ToolWindow), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(OnToolWindowChanged));

        public bool ToolWindow {
            get => (bool)GetValue(ToolWindowProperty);
            set => SetValue(ToolWindowProperty, value);
        }

        private static void OnToolWindowChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DpiAwareWindow)o).OnToolWindowChanged((bool)e.NewValue);
        }

        private void OnToolWindowChanged(bool newValue) {
            try {
                if (!IsLoaded) return;
                var handle = new WindowInteropHelper(this).Handle;
                var current = NativeMethods.GetWindowExStyle(handle);
                NativeMethods.SetWindowExStyle(handle,
                        newValue ? current | WindowExStyle.ToolWindow : current & ~WindowExStyle.ToolWindow);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void SetExtraFlagsIfNeeded() {
            if (PreventActivation) {
                OnPreventActivationChanged(true);
            }

            if (ToolWindow) {
                OnPreventActivationChanged(true);
            }
        }
    }
}