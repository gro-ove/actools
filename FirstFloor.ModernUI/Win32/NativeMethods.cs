using System;
using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    internal static class NativeMethods {
        public const int SOk = 0;
        public const int WmDpiChanged = 0x02E0;
        public const int MonitorDefaultToNearest = 0x00000002;
        public const int WsExNoActivate = 0x08000000;
        public const int GwlExStyle = -20;

        [DllImport("Shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr hprocess, out ProcessDpiAwareness value);

        [DllImport("Shcore.dll")]
        public static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("user32.dll")]
        public static extern bool IsProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern int SetProcessDPIAware();

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, ref uint xDpi, ref uint yDpi);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flag);

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [Flags]
        public enum DwmBb {
            DwmBbEnable = 1
        }

        public const int WmDwmCompositionChanged = 0x031E;

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern bool DwmIsCompositionEnabled();

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableBlurBehindWindow(IntPtr hwnd, ref DwmBlurBehind blurBehind);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMargins);

        [StructLayout(LayoutKind.Sequential)]
        public struct DwmBlurBehind {
            public DwmBb Flags;
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
