using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

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

            public DelegateCommand ManageListsCommand => _manageListsCommand ?? (_manageListsCommand
                    = new DelegateCommand(() => new OnlineListsManager().ShowDialog()));

            private DelegateCommand _manageDriverTagsCommand;

            public DelegateCommand ManageDriversTagsCommand => _manageDriverTagsCommand ?? (_manageDriverTagsCommand
                    = new DelegateCommand(() => new OnlineDriverTags().ShowDialog()));

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
            
            public BetterListCollectionView LobbiesBuiltIn { get; }
            public BetterListCollectionView LobbiesUser { get; }

            private ThirdPartyOnlineSource _selectedUserLobby;

            public ThirdPartyOnlineSource SelectedUserLobby {
                get => _selectedUserLobby;
                set => Apply(value, ref _selectedUserLobby);
            }

            private AsyncCommand _addUserLobbyCommand;

            public AsyncCommand AddUserLobbyCommand => _addUserLobbyCommand ?? (_addUserLobbyCommand = new AsyncCommand(async () => {
                var url = await Prompt.ShowAsync("Lobby list URL:", "Add new lobby list",
                        comment: "Choose an URL with the lobby list.");
                if (url == null) return;

                if (ThirdPartyOnlineSourcesManager.Instance.List.Any(x => x.Url == url)) {
                    MessageDialog.Show("Service with this URL is already added", "Can’t add new service", MessageDialogButton.OK);
                    return;
                }

                await Task.Delay(100);
                var name = await Prompt.ShowAsync("Lobby name:", "Add new lobby lists", url.GetDomainNameFromUrl(),
                        comment: "Pick a name for its link in Online section.");
                if (name != null) {
                    ThirdPartyOnlineSourcesManager.Instance.List.Add(new ThirdPartyOnlineSource(false, url, name));
                    ThirdPartyOnlineSourcesManager.Instance.SaveUserLobbies();
                }
            }));

            private DelegateCommand _deleteSelectedUserLobbyCommand;

            public DelegateCommand DeleteSelectedUserLobbyCommand => _deleteSelectedUserLobbyCommand ?? (_deleteSelectedUserLobbyCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage($"Are you sure to remove {SelectedUserLobby.DisplayName}?",
                        "Remove lobby", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }
                SelectedUserLobby.Dispose();
                ThirdPartyOnlineSourcesManager.Instance.List.Remove(SelectedUserLobby);
                SelectedUserLobby = null;
                ThirdPartyOnlineSourcesManager.Instance.SaveUserLobbies();
            }));

            private DelegateCommand _shareSelectedUserLobbyCommand;

            public DelegateCommand ShareSelectedUserLobbyCommand => _shareSelectedUserLobbyCommand ?? (_shareSelectedUserLobbyCommand = new DelegateCommand(() => {
                var link = $@"{InternalUtils.MainApiDomain}/s/q:lobby?name={Uri.EscapeDataString(SelectedUserLobby.DisplayName)}&url={Uri.EscapeDataString(SelectedUserLobby.Url)}";
                if (!string.IsNullOrWhiteSpace(SelectedUserLobby.Description)) {
                    link += $"&description={Uri.EscapeDataString(SelectedUserLobby.Description)}";
                }
                if (!string.IsNullOrWhiteSpace(SelectedUserLobby.Flags)) {
                    link += $"&flags={Uri.EscapeDataString(SelectedUserLobby.Flags)}";
                }
                SharingUiHelper.ShowShared("Online lobby", link, false);
            }));
                    
            public ViewModel() {
                NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(
                        x => x.GetIPProperties().UnicastAddresses.Any(y => y.Address.AddressFamily == AddressFamily.InterNetwork)).ToList();
                ThirdPartyOnlineSourcesManager.Instance.Initialize();
                LobbiesBuiltIn = new BetterListCollectionView(ThirdPartyOnlineSourcesManager.Instance.List) {
                    Filter = x => (x as ThirdPartyOnlineSource)?.IsBuiltIn == true
                };
                LobbiesUser = new BetterListCollectionView(ThirdPartyOnlineSourcesManager.Instance.List) {
                    Filter = x => (x as ThirdPartyOnlineSource)?.IsBuiltIn == false
                };
            }
        }

        private void OnUserLinkTextChanged(object sender, TextChangedEventArgs e) {
            ThirdPartyOnlineSourcesManager.Instance.SaveUserLobbies();
        }
    }
}