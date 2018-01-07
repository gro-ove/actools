using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
// ReSharper disable InconsistentNaming

namespace AcTools.Windows {
    public static class User32 {
        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        public delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.Dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr AttachThreadInput(IntPtr idAttach, IntPtr idAttachTo, int fAttach);

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [StructLayout(LayoutKind.Sequential)]
        public struct Win32Point {
            public int X;
            public int Y;
        };

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hwnd, ref Win32Point pt);

        public static bool IsKeyPressed(int keyCode) => (GetAsyncKeyState((Keys)keyCode) & 0x8000) != 0;

        public static bool IsKeyPressed(Keys vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

        public static short GetAsyncKeyState(int keyCode) => GetAsyncKeyState((Keys)keyCode);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);

        public const int HC_ACTION = 0;

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

        public static int GwlStyle = -16;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, WindowShowStyle nCmdShow);

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

        public const int WM_KEYDOWN = 0x100;
        public const int WM_KEYUP = 0x101;
        public const int WM_SYSCOMMAND = 0x018;
        public const int SC_CLOSE = 0x053;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr PostMessage(HandleRef hwnd, int msg, int wparam, int lparam);

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
            public int VirtualKeyCode;
            public int ScanCode;
            public int Flags;
            public int Time;
            public int ExtraInfo;
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

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE {
            public const int CCHDEVICENAME = 0x20;
            public const int CCHFORMNAME = 0x20;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private const uint WM_GETTEXT = 0x000D;

        public static string GetText(IntPtr handle) {
            var message = new StringBuilder(1000);
            SendMessage(handle, WM_GETTEXT, message.Capacity, message);
            return message.ToString();
        }
    }
}
