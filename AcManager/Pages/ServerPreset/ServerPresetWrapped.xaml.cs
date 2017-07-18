using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetWrapped {
        public ServerPresetWrapped() {
            InitializeComponent();
            this.AddWidthCondition(800).Add(x => {
                Grid.StackMode = !x;
                ScrollViewer.VerticalScrollBarVisibility = x ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
                Grid.Columns = x ? 2 : 1;
            });
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
