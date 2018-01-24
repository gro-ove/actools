using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Xml;
using System.Xml.Serialization;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public static class WindowPlacement {
        // RECT structure required by WINDOWPLACEMENT structure
        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct WindowPlacementRect {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public WindowPlacementRect(int left, int top, int right, int bottom) {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public override string ToString() {
                return $"(Left={Left}, Top={Top}, Width={Width}, Height={Height})";
            }
        }

        // POINT structure required by WINDOWPLACEMENT structure
        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct WindowPlacementPoint {
            public int X;
            public int Y;

            public WindowPlacementPoint(int x, int y) {
                X = x;
                Y = y;
            }

            public override string ToString() {
                return $"({X}, {Y})";
            }
        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct WindowPlacementStruct {
            public int length;
            public int flags;
            public int showCmd;
            public WindowPlacementPoint minPosition;
            public WindowPlacementPoint maxPosition;
            public WindowPlacementRect normalPosition;

            public WindowPlacementRect Rect => normalPosition;

            public override string ToString() {
                return $"Normal placement: {normalPosition}";
            }
        }

        private static readonly Encoding Encoding = new UTF8Encoding();
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(WindowPlacementStruct));

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacementStruct lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacementStruct lpwndpl);

        public const int ShowStateNormal = 1;
        public const int ShowStateMinimized = 1;

        private static void SetPlacementStruct(IntPtr windowHandle, WindowPlacementStruct placement) {
            try {
                placement.length = Marshal.SizeOf(typeof(WindowPlacementStruct));
                placement.flags = 0;
                placement.showCmd = placement.showCmd == ShowStateMinimized ? ShowStateNormal : placement.showCmd;
                SetWindowPlacement(windowHandle, ref placement);
            } catch (InvalidOperationException e) {
                Logging.Error(e);
            }
        }

        private static WindowPlacementStruct GetPlacementStruct(IntPtr windowHandle) {
            GetWindowPlacement(windowHandle, out var placement);
            return placement;
        }

        public static void SetPlacementRect(this Window window, Rect rect) {
            var handle = new WindowInteropHelper(window).Handle;
            var placement = GetPlacementStruct(handle);
            placement.normalPosition = new WindowPlacementRect {
                Left = (int)rect.Left,
                Top = (int)rect.Top,
                Right = (int)rect.Right,
                Bottom = (int)rect.Bottom
            };
            SetPlacementStruct(handle, placement);
        }

        public static Rect GetPlacementRect(this Window window) {
            var rect = GetPlacementStruct(new WindowInteropHelper(window).Handle).Rect;
            return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        private static string GetPlacement(IntPtr windowHandle) {
            using (var memoryStream = new MemoryStream()) {
                using (var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8)) {
                    Serializer.Serialize(xmlTextWriter, GetPlacementStruct(windowHandle));
                    var xmlBytes = memoryStream.ToArray();
                    return Encoding.GetString(xmlBytes);
                }
            }
        }

        private static void SetPlacement(IntPtr windowHandle, [CanBeNull] string placementXml) {
            if (string.IsNullOrEmpty(placementXml)) return;
            try {
                using (var memoryStream = new MemoryStream(Encoding.GetBytes(placementXml))) {
                    SetPlacementStruct(windowHandle, (WindowPlacementStruct)Serializer.Deserialize(memoryStream));
                }
            } catch (InvalidOperationException e) {
                Logging.Error(e);
            }
        }

        public static string GetPlacement(this Window window) {
            return GetPlacement(new WindowInteropHelper(window).Handle);
        }

        public static void SetPlacement(this Window window, [CanBeNull] string placementXml) {
            SetPlacement(new WindowInteropHelper(window).Handle, placementXml);
        }

        public static bool IsWindowOnScreen(this Window window, Screen screen, bool autoAdjustWindow = true) {
            var placement = window.GetPlacementRect();
            var width = placement.Width;
            var height = placement.Height;
            var left = placement.Left;
            var top = placement.Top;
            Logging.Debug($"Screen: {screen}, window: {placement}");

            var leftTest = left >= screen.Bounds.Left;
            var topTest = top >= screen.Bounds.Top;
            var bottomTest = top + height <= screen.Bounds.Bottom;
            var rightTest = left + width <= screen.Bounds.Right;
            if (leftTest && topTest && bottomTest && rightTest) return true;
            Logging.Debug($"Test: {leftTest}, {topTest}, {bottomTest}, {rightTest}");

            if (autoAdjustWindow) {
                if (!bottomTest){
                    Logging.Debug($"Auto-adjust bottom: {screen.Bounds.Bottom - height}");
                    window.Top = screen.Bounds.Bottom - height;
                }

                if (!topTest){
                    Logging.Debug($"Auto-adjust top: {screen.Bounds.Top}");
                    window.Top = screen.Bounds.Top;
                }
                if (!leftTest){
                    Logging.Debug($"Auto-adjust left: {screen.Bounds.Left}");
                    window.Left = screen.Bounds.Left;
                }

                if (!rightTest){
                    Logging.Debug($"Auto-adjust right: {screen.Bounds.Right - width}");
                    window.Left = screen.Bounds.Right - width;
                }
            }

            return false;
        }

        public static bool IsWindowOnAnyScreen(this Window window, bool autoAdjustWindow = true) {
            return window.IsWindowOnScreen(Screen.FromHandle(new WindowInteropHelper(window).Handle), autoAdjustWindow);
        }
    }
}