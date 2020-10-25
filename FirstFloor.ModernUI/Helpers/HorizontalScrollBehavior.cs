using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Helpers {
    public class HorizontalScrollBehavior : Behavior<ItemsControl> {
        private ScrollViewer _scrollViewer;

        public bool IsInverted { get; set; }

        public bool IsEnabled { get; set; } = true;

        protected override void OnAttached() {
            base.OnAttached();
            AssociatedObject.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            AssociatedObject.Loaded -= OnLoaded;

            _scrollViewer = AssociatedObject.FindVisualChild<ScrollViewer>();
            if (_scrollViewer != null) {
                _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            }
        }

        protected override void OnDetaching() {
            base.OnDetaching();

            if (_scrollViewer != null) {
                _scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!e.Handled && IsEnabled) {
                _scrollViewer.ScrollToHorizontalOffset(IsInverted ?
                        _scrollViewer.HorizontalOffset + e.Delta :
                        _scrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}