using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class ScrollPropagation {
        public static bool GetFix(DependencyObject obj) {
            return (bool)obj.GetValue(FixProperty);
        }

        public static void SetFix(DependencyObject obj, bool value) {
            obj.SetValue(FixProperty, value);
        }

        public static readonly DependencyProperty FixProperty = DependencyProperty.RegisterAttached("Fix", typeof(bool),
                typeof(ScrollPropagation), new UIPropertyMetadata(OnFixChanged));

        private static void OnFixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FrameworkElement element && e.NewValue is bool value) {
                if (value) {
                    element.PreviewMouseWheel += OnMouseWheel;
                } else {
                    element.PreviewMouseWheel -= OnMouseWheel;
                }
            }
        }

        private static void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if (sender is ScrollViewer scrollViewer) {
                var hovered = (Mouse.DirectlyOver as FrameworkElement)?.GetParent<ScrollViewer>();
                if (hovered != null && hovered != scrollViewer) {
                    if (e.Delta < 0
                            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
                            : hovered.VerticalOffset == 0) {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                        e.Handled = true;
                    }
                }
            } else {
                if (((Control)sender).Parent is UIElement parent)
                {
                    e.Handled = true; // Prevent ListBox from handling it
                    var newEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = sender
                    };
                    parent.RaiseEvent(newEvent); // Bubble up to parent
                }
            }
        }
    }
}