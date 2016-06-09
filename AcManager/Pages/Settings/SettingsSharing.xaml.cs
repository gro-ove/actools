using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsSharing {
        public class SharingViewModel : NotifyPropertyChanged {
            public SettingsHolder.SharingSettings Sharing => SettingsHolder.Sharing;

            public BetterObservableCollection<SharedEntry> History => SharingHelper.Instance.History;
        }

        public SettingsSharing() {
            InitializeComponent();
            DataContext = new SharingViewModel();
        }

        private void ScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void History_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var value = HistoryDataGrid.SelectedValue as SharedEntry;
            if (value != null) {
                Process.Start(value.Url + "#noauto");
            }
        }
    }
}
