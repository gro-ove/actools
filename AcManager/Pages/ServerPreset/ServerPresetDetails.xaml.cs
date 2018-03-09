using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetDetails {
        public ServerPresetDetails() {
            InitializeComponent();
            this.AddWidthCondition(800).Add(x => {
                Grid.VerticalStackMode = !x;
                ScrollViewer.VerticalScrollBarVisibility = x ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
                ScrollViewerLeft.VerticalScrollBarVisibility = ScrollViewerRight.VerticalScrollBarVisibility =
                        x ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                Grid.Columns = x ? 2 : 1;
            });
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if (ScrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled) {
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}
