using System.Windows.Input;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetCombined {
        public ServerPresetCombined() {
            InitializeComponent();
            PreviewMouseWheel += OnMouseWheel;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
