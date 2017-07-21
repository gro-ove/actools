using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.LargeFilesSharing;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using BooleanSwitch = FirstFloor.ModernUI.Windows.Controls.BooleanSwitch;

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
            var value = HistoryDataGrid.SelectedValue as SharedEntry;
            if (value != null) {
                Process.Start(value.Url + "#noauto");
            }
        }
    }
}
