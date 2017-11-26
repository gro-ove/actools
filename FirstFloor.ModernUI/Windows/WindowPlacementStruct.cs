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
        }

        private static readonly Encoding Encoding = new UTF8Encoding();
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(WindowPlacementStruct));

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacementStruct lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacementStruct lpwndpl);

        public const int ShowStateNormal = 1;
        public const int ShowStateMinimized = 1;

        public static void SetPlacement(IntPtr windowHandle, [CanBeNull] string placementXml) {
            if (string.IsNullOrEmpty(placementXml)) {
                return;
            }

            var xmlBytes = Encoding.GetBytes(placementXml);

            try {
                WindowPlacementStruct placement;
                using (var memoryStream = new MemoryStream(xmlBytes)) {
                    placement = (WindowPlacementStruct)Serializer.Deserialize(memoryStream);
                }

                placement.length = Marshal.SizeOf(typeof(WindowPlacementStruct));
                placement.flags = 0;
                placement.showCmd = placement.showCmd == ShowStateMinimized ? ShowStateNormal : placement.showCmd;
                SetWindowPlacement(windowHandle, ref placement);
            } catch (InvalidOperationException e) {
                Logging.Error(e);
                // Parsing placement XML failed. Fail silently.
            }
        }

        public static string GetPlacement(IntPtr windowHandle) {
            GetWindowPlacement(windowHandle, out var placement);
            using (var memoryStream = new MemoryStream()) {
                using (var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8)) {
                    Serializer.Serialize(xmlTextWriter, placement);
                    var xmlBytes = memoryStream.ToArray();
                    return Encoding.GetString(xmlBytes);
                }
            }
        }

        public static void SetPlacement(this Window window, [CanBeNull] string placementXml) {
            SetPlacement(new WindowInteropHelper(window).Handle, placementXml);
        }

        public static string GetPlacement(this Window window) {
            return GetPlacement(new WindowInteropHelper(window).Handle);
        }

        public static bool IsWindowOnAnyScreen(this Window window, bool autoAdjustWindow = true) {
            var width = (int)window.ActualWidth;
            var height = (int)window.ActualHeight;
            var screen = Screen.FromHandle(new WindowInteropHelper(window).Handle);

            var leftTest = window.Left >= screen.WorkingArea.Left;
            var topTest = window.Top >= screen.WorkingArea.Top;
            var bottomTest = window.Top + height <= screen.WorkingArea.Bottom;
            var rightTest = window.Left + width <= screen.WorkingArea.Right;
            if (leftTest && topTest && bottomTest && rightTest) return true;

            if (autoAdjustWindow) {
                if (!leftTest) window.Left = window.Left - (window.Left - screen.WorkingArea.Left);
                if (!topTest) window.Top = window.Top - (window.Top - screen.WorkingArea.Top);
                if (!bottomTest) window.Top = window.Top - (window.Top + height - screen.WorkingArea.Bottom);
                if (!rightTest) window.Left = window.Left - (window.Left + width - screen.WorkingArea.Right);
            }

            return false;
        }
    }
}