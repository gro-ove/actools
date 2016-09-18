using System.Windows;
using System.Windows.Input;

namespace AcManager.Pages.About {
    public partial class Credits {
        public Credits() {
            InitializeComponent();
        }

        private void TreeView_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            e.Handled = true;
            (((FrameworkElement)sender).Parent as UIElement)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            });
        }
    }
}
