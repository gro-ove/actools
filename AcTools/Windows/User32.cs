using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
// ReSharper disable InconsistentNaming

namespace AcTools.Windows {
    public static class User32 {
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        public static bool IsWindowInForeground(IntPtr hWnd) {
            return hWnd == GetForegroundWindow();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT {
            public int cbData;
            public IntPtr dwData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpData;
        }

        public static COPYDATASTRUCT CopyDataFromString(string str) {
            return new COPYDATASTRUCT {
                dwData = new IntPtr(3),
                cbData = str.Length + 1,
                lpData = str
            };
        }

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

        public enum WindowShowStyle : uint {
            Hide = 0,
            ShowNormal = 1,
            ShowMinimized = 2,
            ShowMaximized = 3,
            Maximize = 3,
            ShowNormalNoActivate = 4,
            Show = 5,
            Minimize = 6,
            ShowMinNoActivate = 7,
            ShowNoActivate = 8,
            Restore = 9,
            ShowDefault = 10,
            ForceMinimized = 11
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        public static extern bool CloseWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        public const uint WM_KEYDOWN = 0x100;
        public const uint WM_KEYUP = 0x101;
        public const uint WM_SYSCOMMAND = 0x018;
        public const uint SC_CLOSE = 0x053;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, ref COPYDATASTRUCT lParam);

        public static void PressKey(IntPtr h, Keys key) {
            PostMessage(h, WM_KEYDOWN, (int)key, 0);
            Thread.Sleep(100);
            PostMessage(h, WM_KEYUP, (int)key, 0);
        }

        public const int MOUSEEVENTF_MOVE = 0x01;
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        public static void MouseEvent(int dwFlags, int dx = 0, int dy = 0, int dwData = 0, int dwExtraInfo = 0) {
            mouse_event(dwFlags, dx, dy, dwData, dwExtraInfo);
        }

        public static void MouseClick(int x, int y) {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        public const int WH_KEYBOARD_LL = 13,
                            WH_KEYBOARD = 2,
                            WM_SYSKEYDOWN = 0x104,
                            WM_SYSKEYUP = 0x105;

        public const byte VK_SHIFT = 0x10,
                            VK_CAPITAL = 0x14,
                            VK_NUMLOCK = 0x90;

        public const int KEYEVENTF_EXTENDEDKEY = 0x1,
                            KEYEVENTF_KEYUP = 0x2;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardHookStruct {
            public int VirtualKeyCode, ScanCode, Flags, Time, ExtraInfo;
        }

        public delegate int HookProc(int nCode, int wParam, IntPtr lParam);

        public static void BringProcessWindowToFront(Process process) {
            if (process == null) return;
            var handle = process.MainWindowHandle;

            for (var i = 0; !IsWindowInForeground(handle); i++) {
                if (i == 0) {
                    Thread.Sleep(150);
                }

                if (IsIconic(handle)) {
                    ShowWindow(handle, WindowShowStyle.Restore);
                } else {
                    SetForegroundWindow(handle);
                }
                Thread.Sleep(250);

                if (IsWindowInForeground(handle)) {
                    Thread.Sleep(500);
                    return;
                }

                if (i > 120) throw new Exception("Could not set process window to the foreground");
            }
        }
    }
}
