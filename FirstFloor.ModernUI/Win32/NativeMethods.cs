using System;
using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    internal static class NativeMethods {
        public const int SOk = 0;
        public const int MonitorDefaultToNearest = 0x00000002;

        [DllImport("Shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr hprocess, out ProcessDpiAwareness value);

        [DllImport("Shcore.dll")]
        public static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("user32.dll")]
        public static extern bool IsProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern int SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint xDpi, out uint yDpi);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flag);

        [DllImport(@"user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Win32Rect lpWindowRect);

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, WindowFlagsSet flagsSet);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, WindowFlagsSet flagsSet, int newStyle);

        public static WindowStyle GetWindowStyle(IntPtr hwnd) {
            return (WindowStyle)GetWindowLong(hwnd, WindowFlagsSet.Style);
        }

        public static void SetWindowStyle(IntPtr hwnd, WindowStyle style) {
            SetWindowLong(hwnd, WindowFlagsSet.Style, (int)style);
        }

        public static WindowExStyle GetWindowExStyle(IntPtr hwnd) {
            return (WindowExStyle)GetWindowLong(hwnd, WindowFlagsSet.ExStyle);
        }

        public static void SetWindowExStyle(IntPtr hwnd, WindowExStyle style) {
            SetWindowLong(hwnd, WindowFlagsSet.ExStyle, (int)style);
        }

        [Flags]
        public enum DwmFlags {
            BlurBackground = 1
        }

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern bool DwmIsCompositionEnabled();

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableBlurBehindWindow(IntPtr hwnd, ref DwmBlurBehind blurBehind);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMargins);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MoveWindow(IntPtr hwnd, int x, int y, int width, int height, [MarshalAs(UnmanagedType.Bool)] bool repaint);

        [StructLayout(LayoutKind.Sequential)]
        public struct DwmBlurBehind {
            public DwmFlags Flags;
            public bool Enable;
            public IntPtr RgnBlur;
            public bool TransitionOnMaximized;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }
    }
}
