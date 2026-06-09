using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsLive : IParametrizedUriContent {
        private bool _changed;

        public SettingsLive() {
            InitializeComponent();
            DataContext = new ViewModel();
            Unloaded += (sender, args) => {
                if (_changed) {
                    Model.Live.UserEntries = Model.Live.UserEntries.ToList();
                    _changed = false;
                }
            };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.LiveSettings Live => SettingsHolder.Live;

            private SettingsHolder.LiveSettings.LiveServiceEntry _selectedLiveService;

            public SettingsHolder.LiveSettings.LiveServiceEntry SelectedLiveService {
                get => _selectedLiveService;
                set => Apply(value, ref _selectedLiveService);
            }

            private DelegateCommand _deleteSelectedServiceCommand;

            public DelegateCommand DeleteSelectedServiceCommand => _deleteSelectedServiceCommand ?? (_deleteSelectedServiceCommand
                    = new DelegateCommand(() => {
                        if (ModernDialog.ShowMessage($"Are you sure to remove {SelectedLiveService.DisplayName}?",
                                "Remove service", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                            return;
                        }
                        Live.UserEntries = Live.UserEntries.ApartFrom(SelectedLiveService).ToList();
                        SelectedLiveService = null;
                    }));

            private DelegateCommand _shareSelectedServiceCommand;

            public DelegateCommand ShareSelectedServiceCommand => _shareSelectedServiceCommand ?? (_shareSelectedServiceCommand
                    = new DelegateCommand(() => {
                        var link = $@"{InternalUtils.MainApiDomain}/s/q:live?name={Uri.EscapeDataString(SelectedLiveService.DisplayName)}&url={Uri.EscapeDataString(SelectedLiveService.Url)}";
                        if (SelectedLiveService.HighlightColor.HasValue) {
                            link += $"&color={Uri.EscapeDataString(SelectedLiveService.HighlightColor.Value.ToHexString())}";
                        }
                        SharingUiHelper.ShowShared("Live service", link, false);
                    }, () => true));

            private AsyncCommand _addLiveServiceCommand;

            public AsyncCommand AddLiveServiceCommand => _addLiveServiceCommand ?? (_addLiveServiceCommand = new AsyncCommand(async () => {
                var url = await Prompt.ShowAsync("Live service URL:", "Add new live service",
                        comment: "Choose an URL which will open by default when you visit the service.");
                if (url == null) return;

                if (Live.UserEntries.Any(x => x.Url == url)) {
                    MessageDialog.Show("Service with this URL is already added", "Can’t add new service", MessageDialogButton.OK);
                    return;
                }

                string title;
                using (WaitingDialog.Create("Checking URL…")) {
                    title = await PageTitleFinder.GetPageTitle(url);
                }

                await Task.Delay(100);
                var name = await Prompt.ShowAsync("Live service name:", "Add new live service", title,
                        comment: "Pick a name for its link in Live Services section.");
                if (name != null) {
                    Live.UserEntries = Live.UserEntries.Append(new SettingsHolder.LiveSettings.LiveServiceEntry(url, name, null)).ToList();
                }
            }));
        }

        public void OnUri(Uri uri) {
            if (uri.GetQueryParamBool("Separate")) {
                ContentRoot.Margin = new Thickness(20d);
            }
        }

        private void OnUserLinkTextChanged(object sender, TextChangedEventArgs e) {
            _changed = true;
        }
    }
}