using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace FirstFloor.ModernUI.Helpers {
    public class HorizontalScrollViewerBehavior : Behavior<ScrollViewer> {
        public bool IsInverted { get; set; }

        public bool IsEnabled { get; set; } = true;

        protected override void OnAttached() {
            base.OnAttached();
            AssociatedObject.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        protected override void OnDetaching() {
            AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
            base.OnDetaching();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!e.Handled && IsEnabled) {
                AssociatedObject?.ScrollToHorizontalOffset(IsInverted ?
                        AssociatedObject.HorizontalOffset + e.Delta :
                        AssociatedObject.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}