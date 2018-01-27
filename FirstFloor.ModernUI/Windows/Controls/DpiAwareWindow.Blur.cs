using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Win32;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public static readonly DependencyProperty BlurBackgroundProperty = DependencyProperty.Register(nameof(BlurBackground), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(OnBlurBackgroundChanged));

        public bool BlurBackground {
            get => (bool)GetValue(BlurBackgroundProperty);
            set => SetValue(BlurBackgroundProperty, value);
        }

        private static void OnBlurBackgroundChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DpiAwareWindow)o).OnBlurBackgroundChanged((bool)e.NewValue);
        }

        private void OnBlurBackgroundChanged(bool newValue) {
            try {
                if (!IsLoaded) return;
                Controller.Value?.Set(new WindowInteropHelper(this).Handle, newValue);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void SetBackgroundBlurIfNeeded() {
            if (BlurBackground) {
                OnBlurBackgroundChanged(true);
            }
        }

        private static readonly Lazy<IBackgroundBlur> Controller = new Lazy<IBackgroundBlur>(Create);

        [CanBeNull]
        private static IBackgroundBlur Create() {
            // Blurring was re-introduced
            if (WindowsVersionHelper.IsWindows10OrGreater) return new ModernBackgroundBlur();

            // Flat UI with no whistlefarts whatsoever
            if (WindowsVersionHelper.IsWindows8OrGreater) return null;

            // Windows aero!
            if (WindowsVersionHelper.IsWindowsVistaOrGreater) return new AeroBackgroundBlur();

            // Boring Windowses like Windows XP
            return null;
        }

        private interface IBackgroundBlur {
            void Set(IntPtr window, bool value);
        }

        private class AeroBackgroundBlur : IBackgroundBlur {
            private IntPtr WndProc(IntPtr window, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
                if (msg == (int)WindowMessage.DwmCompositionChanged) {
                    Set(window, true);
                }

                return IntPtr.Zero;
            }

            public void Set(IntPtr window, bool enabled) {
                if (!NativeMethods.DwmIsCompositionEnabled()) return;

                if (enabled) {
                    HwndSource.FromHwnd(window)?.AddHook(WndProc);
                } else {
                    HwndSource.FromHwnd(window)?.RemoveHook(WndProc);
                }

                var margins = new NativeMethods.Margins { Left = -1, Right = -1, Bottom = -1, Top = -1 };
                var blurBehind = new NativeMethods.DwmBlurBehind { Enable = enabled, Flags = NativeMethods.DwmFlags.BlurBackground };
                NativeMethods.DwmExtendFrameIntoClientArea(window, ref margins);
                NativeMethods.DwmEnableBlurBehindWindow(window, ref blurBehind);
            }
        }

        private class ModernBackgroundBlur : IBackgroundBlur {
            public void Set(IntPtr window, bool enabled) {
                var state = enabled ? AccentState.AccentEnableBlurbehind : AccentState.AccentDisabled;
                var ptr = Marshal.AllocHGlobal(AccentPolicy.Size);
                Marshal.StructureToPtr(new AccentPolicy { AccentState = state }, ptr, false);

                var data = new WindowCompositionAttributeData {
                    Attribute = WindowCompositionAttribute.WcaAccentPolicy,
                    SizeOfData = AccentPolicy.Size,
                    Data = ptr
                };

                NativeMethods.SetWindowCompositionAttribute(window, ref data);
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}