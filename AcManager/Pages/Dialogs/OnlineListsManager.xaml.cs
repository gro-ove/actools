using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Pages.Drive;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class OnlineListsManager {
        private ViewModel Model => (ViewModel)DataContext;

        public OnlineListsManager() {
            DataContext = new ViewModel();
            InitializeComponent();
            Buttons = new[] {
                CreateExtraDialogButton("Add New List", () => {
                    var name = Prompt.Show("Name for a new servers list:", "Add New Servers List", required: true, maxLength: 255, watermark: @"?");
                    if (!string.IsNullOrWhiteSpace(name)) {
                        FileBasedOnlineSources.CreateList(name);
                    }
                }),
                CloseButton
            };
        }

        public sealed class ListEntry : Displayable, IDisposable, IFilter<ServerEntry> {
            private readonly IOnlineSource _source;

            public string Id { get; }

            public bool IsBuiltIn { get; }

            [CanBeNull]
            public OnlineSourceInformation Information { get; }

            private BetterListCollectionView _servers;
            private SourcesPack _pack;
            private Online.ServerEntrySorter _sorting;

            public BetterListCollectionView Servers {
                get {
                    if (_servers == null) {
                        _servers = new BetterListCollectionView(OnlineManager.Instance.List);
                        using (_servers.DeferRefresh()) {
                            _servers.Filter = FilterTest;

                            _sorting = new Online.SortingName();
                            _servers.CustomSort = _sorting;
                        }

                        _pack = OnlineManager.Instance.GetSourcesPack(Id);
                        if (!_pack.LoadingComplete) {
                            // it’s just a list from file, so we don’t need to bother with cancellation
                            _pack.EnsureLoadedAsync();
                        }

                        if (_pack.Status == OnlineManagerStatus.Ready) {
                            StartPinging().Forget();
                        }

                        _pack.Ready += Pack_Ready;

                        OnlineManager.Instance.List.ItemPropertyChanged += List_ItemPropertyChanged;
                    }

                    return _servers;
                }
            }

            private void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (IsAffectedBy(e.PropertyName)) {
                    var server = (ServerEntry)sender;
                    var willBeVisible = Test(server);
                    var wasVisible = _servers.Contains(sender);
                    if (willBeVisible != wasVisible) {
                        _servers.Refresh(sender);
                        return;
                    }
                }

                if (_sorting.IsAffectedBy(e.PropertyName)) {
                    _servers.Refresh(sender);
                }
            }

            private CancellationTokenSource _currentPinging;

            private async Task StartPinging() {
                // Just a little delay to make sure sources pack is loaded
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                StopPinging();

                var cancellation = new CancellationTokenSource();
                try {
                    _currentPinging = cancellation;
                    await OnlineManager.Instance.PingEverything(this, cancellation.Token);
                } finally {
                    if (Equals(_currentPinging, cancellation)) {
                        _currentPinging = null;
                    }

                    cancellation.Dispose();
                }
            }

            private void StopPinging() {
                if (_currentPinging != null) {
                    _currentPinging.Cancel();
                    _currentPinging = null;
                }
            }

            private void Pack_Ready(object sender, EventArgs e) {
                StartPinging().Forget();
            }

            private bool FilterTest(object obj) {
                return Test((ServerEntry)obj);
            }

            public override string DisplayName {
                get { return _source.DisplayName; }
                set {
                    if (Equals(value, _source.DisplayName) || string.IsNullOrWhiteSpace(value)) return;
                    FileBasedOnlineSources.RenameList(Id, value);
                }
            }

            public ListEntry(IOnlineSource source, bool isBuiltIn) {
                _source = source;
                IsBuiltIn = isBuiltIn;

                Id = _source.Id;
                DisplayName = _source.DisplayName;
                Information = IsBuiltIn ? new OnlineSourceInformation(Id) : FileBasedOnlineSources.Instance.GetInformation(Id);
            }

            public void Dispose() {
                OnlineManager.Instance.List.ItemPropertyChanged -= List_ItemPropertyChanged;
                StopPinging();
                DisposeHelper.Dispose(ref _pack);
            }

            string IFilter<ServerEntry>.Source => $@"<of:{Id}>";

            public bool Test(ServerEntry obj) {
                return obj.ReferencedFrom(Id);
            }

            public bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.ReferencesString);
            }

            private AsyncCommand _addServerCommand;

            public AsyncCommand AddServerCommand => _addServerCommand ?? (_addServerCommand = new AsyncCommand(() => Online.AddServerManually(Id)));

            private DelegateCommand _removeListCommand;

            public DelegateCommand RemoveListCommand => _removeListCommand ?? (_removeListCommand = new DelegateCommand(() => {
                if (ShowMessage("List will be removed to the Recycle Bin. Are you sure?", $"Remove List “{DisplayName}”",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    FileBasedOnlineSources.RemoveList(Id);
                }
            }, () => !IsBuiltIn));
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            public BetterObservableCollection<ListEntry> Entries { get; }

            public ViewModel() {
                OnlineManager.EnsureInitialized();
                Entries = new BetterObservableCollection<ListEntry>(
                        FileBasedOnlineSources.Instance.GetBuiltInSources().Select(x => new ListEntry(x, true)).Concat(
                                FileBasedOnlineSources.Instance.GetUserSources().Select(x => new ListEntry(x, false))));
                FileBasedOnlineSources.Instance.Update += OnUpdate;
            }

            private void OnUpdate(object sender, EventArgs e) {
                Entries.ReplaceEverythingBy(
                        FileBasedOnlineSources.Instance.GetBuiltInSources().Select(x => new ListEntry(x, true)).Concat(
                                FileBasedOnlineSources.Instance.GetUserSources().Select(x => new ListEntry(x, false))));
            }

            public void Dispose() {
                FileBasedOnlineSources.Instance.Update -= OnUpdate;
                Entries.DisposeEverything();
            }
        }

        private void OnClosed(object sender, EventArgs e) {
            Model.Dispose();
        }
    }
}
