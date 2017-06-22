using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetCombined {
        public ServerPresetCombined() {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            foreach (var scrollViewer in this.FindLogicalChildren<ScrollViewer>().ApartFrom(ScrollViewer)) {
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollViewer.Background = new SolidColorBrush(Colors.DarkCyan);
            }
        }
    }
}
