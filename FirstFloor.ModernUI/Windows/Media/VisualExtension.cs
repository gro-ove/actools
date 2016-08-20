using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Media {
    public static class VisualExtension {
        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point {
            public readonly int X;
            public readonly int Y;
        };

        [DllImport(@"user32.dll")]
        private static extern bool GetCursorPos(ref Win32Point pt);

        public static Point GetMousePosition(this Visual relativeTo) {
            var mouse = new Win32Point();
            GetCursorPos(ref mouse);
            return relativeTo.PointFromScreen(new Point(mouse.X, mouse.Y));
        }

        public static bool IsMouseOverElement(this Visual target) {
            return VisualTreeHelper.GetDescendantBounds(target).Contains(target.GetMousePosition());
        }

        [CanBeNull]
        public static Visual GetItemVisual(this ItemsControl list, int index) {
            return list.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated
                    ? null : list.ItemContainerGenerator.ContainerFromIndex(index) as Visual;
        }

        public static bool IsMouseOverItem(this ItemsControl list, int index) {
            return list.GetItemVisual(index)?.IsMouseOverElement() == true;
        }

        public static int GetMouseItemIndex(this ItemsControl list) {
            for (var i = 0; i < list.Items.Count; i++) {
                if (list.IsMouseOverItem(i)) {
                    return i;
                }
            }

            return -1;
        }
    }
}