using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Controls;
using System.Windows.Forms;
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

            public DelegateCommand ManageListsCommand
                => _manageListsCommand ?? (_manageListsCommand = new DelegateCommand(() => { new OnlineListsManager().ShowDialog(); }));

            private DelegateCommand _manageDriverTagsCommand;

            public DelegateCommand ManageDriversTagsCommand
                => _manageDriverTagsCommand ?? (_manageDriverTagsCommand = new DelegateCommand(() => { new OnlineDriverTags().ShowDialog(); }));

            public List<NetworkInterface> NetworkInterfaces { get; }

            private DelegateCommand _changeServerPresetsDirectoryCommand;

            public DelegateCommand ChangeServerPresetsDirectoryCommand
                => _changeServerPresetsDirectoryCommand ?? (_changeServerPresetsDirectoryCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        Description = "Pick a new folder to store server presets in",
                        SelectedPath = Online.ServerPresetsDirectory
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        Online.ServerPresetsDirectory = dialog.SelectedPath;
                        WindowsHelper.RestartCurrentApplication();
                    }
                }));

            private DelegateCommand _openServerPresetsDirectoryCommand;

            public DelegateCommand OpenServerPresetsDirectoryCommand => _openServerPresetsDirectoryCommand
                    ?? (_openServerPresetsDirectoryCommand = new DelegateCommand(() => WindowsHelper.ViewDirectory(Online.ServerPresetsDirectory)));

            private DelegateCommand _changeServerLogsDirectoryCommand;

            public DelegateCommand ChangeServerLogsDirectoryCommand
                => _changeServerLogsDirectoryCommand ?? (_changeServerLogsDirectoryCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        Description = "Pick a new folder to store server logs in",
                        SelectedPath = Online.ServerLogsDirectory
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        Online.ServerLogsDirectory = dialog.SelectedPath;
                    }
                }));

            private DelegateCommand _openServerLogsDirectoryCommand;

            public DelegateCommand OpenServerLogsDirectoryCommand => _openServerLogsDirectoryCommand
                    ?? (_openServerLogsDirectoryCommand = new DelegateCommand(() => WindowsHelper.ViewDirectory(Online.ServerPresetsDirectory)));

            public ViewModel() {
                NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(
                        x => x.GetIPProperties().UnicastAddresses.Any(y => y.Address.AddressFamily == AddressFamily.InterNetwork)).ToList();
            }
        }
    }
}