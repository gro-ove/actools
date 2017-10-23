using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsOnline {
        private ViewModel Model => (ViewModel)DataContext;

        public SettingsOnline() {
            DataContext = new ViewModel();
            InitializeComponent();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);

            var list = Model.Online.IgnoredInterfaces;
            foreach (var item in Model.NetworkInterfaces.Where(x => !list.Contains(x.Id)).ToList()) {
                IgnoredInterfacesListBox.SelectedItems.Add(item);
            }
        }

        private void IgnoredInterfacesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = IgnoredInterfacesListBox.SelectedItems.OfType<NetworkInterface>().Select(x => x.Id).ToList();
            Model.Online.IgnoredInterfaces = Model.NetworkInterfaces.Where(x => !selected.Contains(x.Id)).Select(x => x.Id);
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.OnlineSettings Online => SettingsHolder.Online;
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            private DelegateCommand _manageListsCommand;

            public DelegateCommand ManageListsCommand => _manageListsCommand ?? (_manageListsCommand = new DelegateCommand(() => {
                new OnlineListsManager().ShowDialog();
            }));

            private DelegateCommand _manageDriverTagsCommand;

            public DelegateCommand ManageDriversTagsCommand => _manageDriverTagsCommand ?? (_manageDriverTagsCommand = new DelegateCommand(() => {
                new OnlineDriverTags().ShowDialog();
            }));

            public List<NetworkInterface> NetworkInterfaces { get; }

            public ViewModel() {
                NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(
                        x => x.GetIPProperties().UnicastAddresses.Any(y => y.Address.AddressFamily == AddressFamily.InterNetwork)).ToList();
            }
        }
    }
}
