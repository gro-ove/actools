using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AcTools.Utils.Helpers;

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

        public static bool IsKeyPressed(Keys vKey) => (GetKeyState(vKey) & 0x8000) != 0;

        [DllImport("user32.dll")]
        public static extern short GetKeyState(Keys virtualKeyCode);

        public static bool IsAsyncKeyPressed(Keys vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);

        public enum InputType : uint {
            Mouse = 0,
            Keyboard = 1,
            Hardware = 2,
        }

        [Flags]
        public enum KeyboardFlag : uint {
            None = 0,
            ExtendedKey = 1,
            KeyUp = 2,
            Unicode = 4,
            ScanCode = 8
        }

        [Flags]
        public enum MouseFlag : uint {
            Move = 0x0001,
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            XDown = 0x0080,
            XUp = 0x0100,
            VerticalWheel = 0x0800,
            HorizontalWheel = 0x1000,
            VirtualDesk = 0x4000,
            Absolute = 0x8000,
        }

        public struct MouseInput {
            public int X;
            public int Y;
            public uint MouseData;
            public MouseFlag Flags;
            public uint Time;
            public uint ExtraInfo;
        }

        public static bool IsExtendedKey(Keys keyCode) {
            return keyCode == Keys.Menu ||
                    keyCode == Keys.LMenu ||
                    keyCode == Keys.RMenu ||
                    keyCode == Keys.Control ||
                    keyCode == Keys.RControlKey ||
                    keyCode == Keys.Insert ||
                    keyCode == Keys.Delete ||
                    keyCode == Keys.Home ||
                    keyCode == Keys.End ||
                    keyCode == Keys.Prior ||
                    keyCode == Keys.Next ||
                    keyCode == Keys.Right ||
                    keyCode == Keys.Up ||
                    keyCode == Keys.Left ||
                    keyCode == Keys.Down ||
                    keyCode == Keys.NumLock ||
                    keyCode == Keys.Cancel ||
                    keyCode == Keys.Snapshot ||
                    keyCode == Keys.Divide;
        }

        public struct KeyboardInput {
            public ushort VirtualKeyCode;
            public ushort ScanCode;
            public KeyboardFlag Flags;
            public uint Time;
            public uint ExtraInfo;
        }

        public struct HardwareInput {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Input {
            [FieldOffset(0)]
            public InputType Type;

            [FieldOffset(4)]
            public MouseInput Mouse;

            [FieldOffset(4)]
            public KeyboardInput Keyboard;

            [FieldOffset(4)]
            public HardwareInput Hardware;

            public Input(KeyboardInput keyboardInput) {
                Mouse = default;
                Hardware = default;
                Type = InputType.Keyboard;
                Keyboard = keyboardInput;
            }

            public static readonly int Size = Marshal.SizeOf(typeof(Input));
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

        public static uint SendInput(params KeyboardInput[] input) {
            return SendInput((IEnumerable<KeyboardInput>)input);
        }

        public static uint SendInput(IEnumerable<KeyboardInput> input) {
            var array = input.Select(x => new Input(x)).ToArrayIfItIsNot();
            return SendInput((uint)array.Length, array, Input.Size);
        }

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
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

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
