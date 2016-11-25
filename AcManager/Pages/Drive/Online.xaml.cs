using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls;
using JetBrains.Annotations;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;
using StringBasedFilter;
using Prompt = AcManager.Controls.Dialogs.Prompt;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class Online : ILoadableContent, IParametrizedUriContent {
        private OnlineManagerType _type;
        private BaseOnlineManager _manager;
        private string _filter;

        public void OnUri(Uri uri) {
            _type = uri.GetQueryParamEnum<OnlineManagerType>("Mode");
            _manager = BaseOnlineManager.ManagerByMode(_type);
            _filter = uri.GetQueryParam("Filter");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await _manager.EnsureLoadedAsync();
        }

        public void Load() {
            _manager.EnsureLoadedAsync().Forget();
        }

        public void Initialize() {
            DataContext = new OnlineViewModel(_type, _manager, _filter);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.AddNewServerCommand, new KeyGesture(Key.A, ModifierKeys.Control))
            });
            InitializeComponent();
            ResizingStuff();
        }

        private DispatcherTimer _timer;

        private OnlineViewModel Model => (OnlineViewModel)DataContext;

        private int _timerTick;

        private void Timer_Tick(object sender, EventArgs e) {
            if (++_timerTick > 5) {
                _timerTick = 0;

                if (Model.ServerCombinedFilter?.First?.IsAffectedBy(nameof(ServerEntry.SessionEnd)) == true && Model.Manager != null) {
                    foreach (var serverEntry in Model.Manager.List) {
                        serverEntry.OnSessionEndTick();
                    }
                }
            }

            if (SimpleListMode != ListMode.DetailedPlus) return;
            foreach (var item in ServersListBox.GetVisibleItemsFromListbox<ServerEntry>()) {
                item?.OnTick();
            }
        }

        public enum ListMode {
            [Description("SimpleListItem")]
            Simple,

            [Description("DetailedListItem")]
            Detailed,

            [Description("DetailedPlusListItem")]
            DetailedPlus
        }

        private ListMode _simpleListMode;

        public ListMode SimpleListMode {
            get { return _simpleListMode; }
            set {
                if (Equals(value, _simpleListMode)) return;

                ServersListBox.ItemTemplate = FindResource(value.GetDescription()) as DataTemplate;
                if (value == ListMode.Simple || _simpleListMode == ListMode.Simple) {
                    ServersListBox.ItemContainerStyle =
                            FindResource(value == ListMode.Simple ? @"SimpledListItemContainer" : @"DetailedListItemContainer") as Style;
                }

                _simpleListMode = value;
            }
        }

        private void ResizingStuff() {
            SimpleListMode = ActualWidth > 1080 ? ListMode.DetailedPlus :
                    ActualWidth > 800 ? ListMode.Detailed :
                            ListMode.Simple;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        private bool _loaded;
        private TaskbarProgress _taskbarProgress;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            Model.Manager.PropertyChanged += Manager_PropertyChanged;
            Model.Load();

            _taskbarProgress = new TaskbarProgress();
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += Timer_Tick;

            var scrollViewer = ServersListBox.FindVisualChild<ScrollViewer>();
            var viewer = scrollViewer?.FindVisualChild<Thumb>();

            if (viewer != null) {
                scrollViewer.ScrollChanged += OnScrollChanged;
                viewer.DragStarted += OnScrollStarted;
                viewer.DragCompleted += OnScrollCompleted;
            }
        }

        private bool _scrolling;

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            foreach (var child in ServersListBox.FindVisualChildren<OnlineListBoxDetailedItem>()) {
                child.SetScrolling(_scrolling);
            }
        }

        private void OnScrollStarted(object sender, DragStartedEventArgs e) {
            _scrolling = true;
            foreach (var child in ServersListBox.FindVisualChildren<OnlineListBoxDetailedItem>()) {
                child.SetScrolling(true);
            }
        }

        private void OnScrollCompleted(object sender, DragCompletedEventArgs e) {
            _scrolling = false;
            foreach (var child in ServersListBox.FindVisualChildren<OnlineListBoxDetailedItem>()) {
                child.SetScrolling(false);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            Model.Manager.PropertyChanged -= Manager_PropertyChanged;
            Model.Unload();

            _taskbarProgress?.Dispose();
            _timer?.Stop();
        }

        private void Manager_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            /*switch (e.PropertyName) {
                case nameof(BaseOnlineManager.BackgroundLoading):
                    _taskbarProgress.Set(Model.Manager.BackgroundLoading ? TaskbarState.Indeterminate : TaskbarState.NoProgress);
                    break;

                case nameof(BaseOnlineManager.PingingInProcess):
                    _taskbarProgress.Set(Model.Manager.PingingInProcess ? TaskbarState.Normal : TaskbarState.NoProgress);
                    break;

                case nameof(BaseOnlineManager.Pinged):
                    if (Model.Manager.PingingInProcess) {
                        _taskbarProgress.Set(TaskbarState.Normal);
                        _taskbarProgress.Set(Model.Manager.Pinged, Model.Manager.WrappersList.Count);
                    }
                    break;
            }*/
        }

        public class CombinedFilter<T> : IFilter<T> {
            [CanBeNull]
            public IFilter<T> First { get; }

            public CombinedFilter(IFilter<T> first) {
                First = first;
            }

            [CanBeNull]
            public IFilter<T> Second { get; set; }

            public string Source => First?.Source;

            public bool Test(T obj) {
                return First?.Test(obj) != false && Second?.Test(obj) != false;
            }

            public bool IsAffectedBy(string propertyName) {
                return First?.IsAffectedBy(propertyName) == true || Second?.IsAffectedBy(propertyName) == true;
            }

            public override string ToString() {
                return $@"<{First}>+<{Second}>";
            }
        }

        public class OnlineViewModel : NotifyPropertyChanged {
            public string Key { get; }

            public OnlineManagerType Type { get; set; }

            public BaseOnlineManager Manager { get; }

            public BetterListCollectionView MainList { get; }

            protected readonly IFilter<ServerEntry> ListFilter;

            public bool ShowList => MainList.Count > 0 || SelectedSource != null;

            /*protected override void FilteredNumberChanged(int oldValue, int newValue) {
                if (oldValue == 0 || newValue == 0) {
                    OnPropertyChanged(nameof(ShowList));
                }
            }*/

            private void LoadQuickFilter() {
                var loaded = LimitedStorage.Get(LimitedSpace.OnlineQuickFilter, Key);
                if (loaded == null) return;
                FilterEmpty = loaded.Contains(@"drivers");
                FilterFull = loaded.Contains(@"full");
                FilterPassword = loaded.Contains(@"password");
                FilterMissing = loaded.Contains(@"haserrors");
                FilterBooking = loaded.Contains(@"booking");
            }

            private void SaveQuickFilter() {
                LimitedStorage.Set(LimitedSpace.OnlineQuickFilter, Key, GetQuickFilterString());
            }

            private string GetQuickFilterString() {
                return new[] {
                    FilterEmpty ? @"(drivers>0)" : null,
                    FilterFull ? @"(full-)" : null,
                    FilterPassword ? @"(password-)" : null,
                    FilterMissing ? @"(haserrors-)" : null,
                    FilterBooking ? @"(booking-)" : null
                }.Where(x => x != null).JoinToString('&');
            }

            private IFilter<ServerEntry> CreateQuickFilter() {
                var value = GetQuickFilterString();
                return string.IsNullOrWhiteSpace(value) ? null : Filter.Create(ServerEntryTester.Instance, value);
            }

            private bool _updatingQuickFilter;

            private async void UpdateQuickFilter() {
                if (!_loaded || _updatingQuickFilter) return;
                _updatingQuickFilter = true;

                await Task.Delay(50);
                SaveQuickFilter();
                ServerCombinedFilter.Second = CreateQuickFilter();
                MainList.Refresh();

                if (MainList.CurrentItem == null) {
                    MainList.MoveCurrentToFirst();
                }

                _updatingQuickFilter = false;
            }

            private Uri _selectedSource;

            public Uri SelectedSource {
                get { return _selectedSource; }
                set {
                    if (Equals(value, _selectedSource)) return;
                    _selectedSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowList));
                }
            }

            private static IFilter<ServerEntry> GetFilter(string filterString) {
                return new CombinedFilter<ServerEntry>(filterString == null ? null : Filter.Create(ServerEntryTester.Instance, filterString));
            }

            public CombinedFilter<ServerEntry> ServerCombinedFilter => (CombinedFilter<ServerEntry>)ListFilter;

            public OnlineViewModel(OnlineManagerType type, BaseOnlineManager manager, string filter) {
                ListFilter = GetFilter(filter);
                Key = $@".Online:{type}:{typeof(ServerEntry).Name}:{ListFilter?.Source}";
                MainList = new BetterListCollectionView(manager.List);

                Type = type;
                Manager = manager;

                LoadQuickFilter();
                SortingMode = SortingModes.GetByIdOrDefault(LimitedStorage.Get(LimitedSpace.OnlineSorting, Key)) ?? SortingModes[0];
                ServerCombinedFilter.Second = CreateQuickFilter();
            }

            // private readonly List<IDisposable> _disposeMe = new List<IDisposable>();

            private bool _loaded;

            public void Load() {
                if (_loaded) return;
                _loaded = true;
                
                Manager.List.ItemPropertyChanged += List_ItemPropertyChanged;

                // list.CollectionChanged += List_CollectionChanged;
                // _list.CollectionReady += List_CollectionReady;

                using (MainList.DeferRefresh()) {
                    MainList.Filter = FilterTest;
                    MainList.CustomSort = Sorting;
                }

                LoadCurrent();
                MainList.CurrentChanged += OnCurrentChanged;

                StartPinging().Forget();
            }

            private CancellationTokenSource _currentPinging;

            private async Task StartPinging() {
                StopPinging();
                using (var cancellation = new CancellationTokenSource()) {
                    _currentPinging = cancellation;
                    await Manager.PingEverything(ListFilter, cancellation.Token);
                    if (ReferenceEquals(_currentPinging, cancellation)) {
                        _currentPinging = null;
                    }
                }
            }

            private void StopPinging() {
                _currentPinging?.Cancel();
                _currentPinging = null;
            }

            private void LoadCurrent() {
                var current = ValuesStorage.GetString(Key);

                if (current != null) {
                    var selected = Manager.GetById(current);
                    MainList.MoveCurrentTo(selected);
                } else {
                    MainList.MoveCurrentToFirst();
                }

                CurrentChanged();
            }

            private object _testMeLater;

            private void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (!_loaded) return;

                if (ListFilter.IsAffectedBy(e.PropertyName)) {
                    var server = (ServerEntry)sender;
                    var willBeVisible = ListFilter.Test(server);
                    var wasVisible = MainList.Contains(sender);

                    if (willBeVisible && ReferenceEquals(_testMeLater, sender)) {
                        _testMeLater = null;
                    } else if (willBeVisible != wasVisible) {
                        if (wasVisible && ReferenceEquals(sender, MainList.CurrentItem)) {
                            _testMeLater = sender;
                        } else {
                            _testMeLater = null;
                            MainList.Refresh(sender);
                        }
                        return;
                    }
                }

                if (Sorting.IsAffectedBy(e.PropertyName)) {
                    MainList.Refresh(sender);
                }
            }

            private bool FilterTest(object obj) {
                var s = obj as ServerEntry;
                return s != null && ListFilter.Test(s);
            }

            public void Unload() {
                if (!_loaded) return;
                _loaded = false;
                
                Manager.List.ItemPropertyChanged -= List_ItemPropertyChanged;
                StopPinging();
            }

            private ServerEntrySorter _sorting;

            [NotNull]
            public ServerEntrySorter Sorting {
                get { return _sorting; }
                set {
                    if (Equals(value, _sorting)) return;
                    _sorting = value;
                    OnPropertyChanged();

                    if (_loaded) {
                        MainList.CustomSort = value;
                    }
                }
            }

            private SettingEntry _sortingMode;

            public SettingEntry SortingMode {
                get { return _sortingMode; }
                set {
                    if (!SortingModes.Contains(value)) value = SortingModes[0];
                    if (Equals(value, _sortingMode)) return;

                    _sortingMode = value;
                    OnPropertyChanged();
                    LimitedStorage.Set(LimitedSpace.OnlineSorting, Key, _sortingMode.Id);

                    Sorting = GetSorter(value.Value);
                }
            }

            private CommandBase _changeSortingCommand;

            public ICommand ChangeSortingCommand => _changeSortingCommand ?? (_changeSortingCommand = new DelegateCommand<string>(o => {
                SortingMode = SortingModes.GetByIdOrDefault(o) ?? SortingModes[0];
            }));

            private ICommand _addNewServerCommand;

            public ICommand AddNewServerCommand => _addNewServerCommand ?? (_addNewServerCommand = new DelegateCommand(async () => {
                var address = Prompt.Show(AppStrings.Online_AddServer, AppStrings.Online_AddServer_Title, "", // TODO
                        @"127.0.0.1:8081", AppStrings.Online_AddServer_Tooltip);
                if (address == null) return;

                foreach (var s in address.Split(',').Select(x => x.Trim())) {
                    try {
                        using (var waiting = new WaitingDialog()) {
                            waiting.Report(AppStrings.Online_AddingServer);
                            await RecentManagerOld.Instance.AddServer(s, waiting, waiting.CancellationToken);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify(AppStrings.Online_CannotAddServer, e);
                    }
                }
            }, () => Manager is RecentManagerOld));

            private AsyncCommand _refreshCommand;

            public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(async () => {
                await Manager.ReloadCommand.ExecuteAsync();
                StartPinging().Forget();
            }));

            protected void OnCurrentChanged(object sender, EventArgs e) {
                // base.OnCurrentChanged(sender, e);
                CurrentChanged();
            }

            private void CurrentChanged() {
                var currentId = ((ServerEntry)MainList.CurrentItem)?.Id;
                if (currentId == null) return;

                SelectedSource = UriExtension.Create("/Pages/Drive/Online_SelectedServerPage.xaml?Mode={0}&Id={1}", Type, currentId);
                ValuesStorage.Set(Key, currentId);

                if (_testMeLater != null) {
                    MainList.Refresh(_testMeLater);
                    _testMeLater = null;
                }
            }

            private bool _filterBooking;

            public bool FilterBooking {
                get { return _filterBooking; }
                set {
                    if (Equals(value, _filterBooking)) return;
                    _filterBooking = value;
                    OnPropertyChanged();
                    UpdateQuickFilter();
                }
            }

            private bool _filterEmpty;

            public bool FilterEmpty {
                get { return _filterEmpty; }
                set {
                    if (Equals(value, _filterEmpty)) return;
                    _filterEmpty = value;
                    OnPropertyChanged();
                    UpdateQuickFilter();
                }
            }

            private bool _filterFull;

            public bool FilterFull {
                get { return _filterFull; }
                set {
                    if (Equals(value, _filterFull)) return;
                    _filterFull = value;
                    OnPropertyChanged();
                    UpdateQuickFilter();
                }
            }

            private bool _filterPassword;

            public bool FilterPassword {
                get { return _filterPassword; }
                set {
                    if (Equals(value, _filterPassword)) return;
                    _filterPassword = value;
                    OnPropertyChanged();
                    UpdateQuickFilter();
                }
            }

            private bool _filterMissing = true;

            public bool FilterMissing {
                get { return _filterMissing; }
                set {
                    if (Equals(value, _filterMissing)) return;
                    _filterMissing = value;
                    OnPropertyChanged();
                    UpdateQuickFilter();
                }
            }
        }

        private void ServersListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (SettingsHolder.Content.ScrollAutomatically) {
                var listBox = (ListBox)sender;
                if (listBox.SelectedItem != null) {
                    listBox.ScrollIntoView(listBox.SelectedItem);
                }
            }
        }
    }
}
