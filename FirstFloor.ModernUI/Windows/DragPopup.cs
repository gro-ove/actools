using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows {

    public class DragPopup : Popup, IDisposable {
        private static class WindowHelper {
            [DllImport(@"user32.dll")]
            private static extern int GetWindowLong(IntPtr hwnd, int index);

            [DllImport(@"user32.dll")]
            private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

            private const int WsExTransparent = 0x00000020;
            private const int GwlExstyle = -20;

            public static void SetWindowExTransparent(IntPtr hwnd) {
                SetWindowLong(hwnd, GwlExstyle, GetWindowLong(hwnd, GwlExstyle) | WsExTransparent);
            }
        }

        private readonly double _deviceScaleX = 1d;
        private readonly double _deviceScaleY = 1d;

        public DragPopup(Size size, Brush brush) {
            Child = new Rectangle {
                Fill = brush,
                Width = size.Width,
                Height = size.Height,
                IsHitTestVisible = false,
                Opacity = 0.7
            };

            AllowsTransparency = true;
            IsHitTestVisible = false;
            IsOpen = true;
            StaysOpen = false;
            Placement = PlacementMode.Absolute;
            UpdatePosition();

            if (PresentationSource.FromVisual(Child) is HwndSource hwndSource) {
                if (hwndSource.CompositionTarget != null) {
                    var matrix = hwndSource.CompositionTarget.TransformToDevice;
                    _deviceScaleX = matrix.M11;
                    _deviceScaleY = matrix.M22;
                }

                WindowHelper.SetWindowExTransparent(hwndSource.Handle);
            }

            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object sender, EventArgs e) {
            UpdatePosition();
        }

        public void UpdatePosition() {
            var pos = VisualExtension.GetMousePosition();
            HorizontalOffset = pos.X / _deviceScaleX - 8;
            VerticalOffset = pos.Y / _deviceScaleY - 8;
        }

        public void Dispose() {
            IsOpen = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }
}