using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsOnline {
        private SettingsOnlineViewModel Model => (SettingsOnlineViewModel)DataContext;

        public SettingsOnline() {
            DataContext = new SettingsOnlineViewModel();
            InitializeComponent();

            var list = Model.Holder.IgnoredInterfaces;
            foreach (var item in Model.NetworkInterfaces.Where(x => !list.Contains(x.Id)).ToList()) {
                IgnoredInterfacesListBox.SelectedItems.Add(item);
            }
        }

        private void IgnoredInterfacesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selected = IgnoredInterfacesListBox.SelectedItems.OfType<NetworkInterface>().Select(x => x.Id).ToList();
            Model.Holder.IgnoredInterfaces = Model.NetworkInterfaces.Where(x => !selected.Contains(x.Id)).Select(x => x.Id);
        }

        public class SettingsOnlineViewModel : NotifyPropertyChanged {
            public SettingsHolder.OnlineSettings Holder => SettingsHolder.Online;

            public List<NetworkInterface> NetworkInterfaces { get; }

            public SettingsOnlineViewModel() {
                NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(
                        x => x.GetIPProperties().UnicastAddresses.Any(
                                y => y.Address.AddressFamily == AddressFamily.InterNetwork)).ToList();
            }
        }
    }
}
