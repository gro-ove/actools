using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsSharing {
        public class SharingViewModel : NotifyPropertyChanged {
            public SettingsHolder.SharingSettings Sharing => SettingsHolder.Sharing;
            public BetterObservableCollection<SharedEntry> History => SharingHelper.Instance.History;
        }

        public SettingsSharing() {
            InitializeComponent();
            DataContext = new SharingViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public SharingViewModel Model => (SharingViewModel)DataContext;

        private void OnScrollMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e) {
            if (HistoryDataGrid.SelectedValue is SharedEntry value) {
                Process.Start(value.Url + "#noauto");
            }
        }
    }
}
