using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;
using FirstFloor.ModernUI.Win32;
using JetBrains.Annotations;
using Point = System.Windows.Point;

namespace FirstFloor.ModernUI.Windows.Media {
    public static partial class VisualExtension {
        public static Point GetMousePosition() {
            var mouse = new NativeMethods.Win32Point();
            NativeMethods.GetCursorPos(ref mouse);
            return new Point(mouse.X, mouse.Y);
        }

        public static Point GetMousePosition(this Visual relativeTo) {
            return relativeTo.PointFromScreen(GetMousePosition());
        }

        public static Point GetMousePosition(this Visual relativeTo, Point screenPoint) {
            return relativeTo.PointFromScreen(screenPoint);
        }

        public static bool IsMouseOverElement(this Visual target) {
            return VisualTreeHelper.GetDescendantBounds(target).Contains(target.GetMousePosition());
        }

        public static bool IsMouseOverElement(this Visual target, Point screenPoint) {
            return VisualTreeHelper.GetDescendantBounds(target).Contains(target.GetMousePosition(screenPoint));
        }

        [CanBeNull]
        public static Visual GetItemVisual(this ItemsControl list, object item) {
            return list.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated
                    ? null : list.ItemContainerGenerator.ContainerFromItem(item) as Visual;
        }

        [CanBeNull]
        public static Visual GetIndexVisual(this ItemsControl list, int index) {
            return list.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated
                    ? null : list.ItemContainerGenerator.ContainerFromIndex(index) as Visual;
        }

        public static bool IsMouseOverIndex(this ItemsControl list, int index) {
            return list.GetIndexVisual(index)?.IsMouseOverElement() == true;
        }

        public static bool IsMouseOverIndex(this ItemsControl list, int index, Point point) {
            return list.GetIndexVisual(index)?.IsMouseOverElement(point) == true;
        }

        public static bool IsMouseOverItem(this ItemsControl list, object item) {
            return list.GetItemVisual(item)?.IsMouseOverElement() == true;
        }

        [CanBeNull]
        public static object GetMouseItem(this ItemsControl list) {
            var screenPoint = GetMousePosition();

            for (var i = 0; i < list.Items.Count; i++) {
                if (list.IsMouseOverIndex(i, screenPoint)) {
                    return list.Items[i];
                }
            }

            return null;
        }

        public static int GetMouseItemIndex(this ItemsControl list) {
            var screenPoint = GetMousePosition();

            for (var i = 0; i < list.Items.Count; i++) {
                if (list.IsMouseOverIndex(i, screenPoint)) {
                    return i;
                }
            }

            return -1;
        }

        public static double DistanceTo(this Point a, Point b) {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2d) + Math.Pow(a.Y - b.Y, 2d));
        }

        public static Screen GetDeviceScreen([NotNull] this Window window) {
            return Screen.FromRectangle(new Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height));
        }
    }
}