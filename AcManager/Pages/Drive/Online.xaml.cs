using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    public partial class Online : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            DataContext = new OnlineViewModel(uri.GetQueryParam("Filter"));
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

                if (Model.ListFilter.IsAffectedBy(nameof(ServerEntry.SessionEnd)) && Model.Manager != null) {
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

        private static string GetKey(string value) {
            return $@"Online.{value}";
        }

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.SelectedEntry, GetKey(e.OldValue), GetKey(e.NewValue));
            LimitedStorage.Move(LimitedSpace.OnlineQuickFilter, GetKey(e.OldValue), GetKey(e.NewValue));
            LimitedStorage.Move(LimitedSpace.OnlineSorting, GetKey(e.OldValue), GetKey(e.NewValue));
        }

        public class OnlineViewModel : NotifyPropertyChanged {
            public string Key { get; }

            public OnlineManager Manager { get; }

            public SourcesPack Pack { get; }

            public BetterListCollectionView MainList { get; }

            [NotNull]
            public readonly CombinedFilter<ServerEntry> ListFilter;

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
                ListFilter.Second = CreateQuickFilter();
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
                }
            }

            private class ServerSourceTester : IFilter<ServerEntry> {
                public string Source => _source;

                private readonly string _source;

                public ServerSourceTester(string source) {
                    _source = source;
                }

                public bool Test(ServerEntry obj) {
                    return obj.OriginsFrom(_source);
                }

                public bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.Origins);
                }

                public override string ToString() {
                    return @"[" + Source + @"]";
                }
            }

            private class MultiServerSourceTester : IFilter<ServerEntry> {
                private string _source;

                public string Source => _source ?? (_source = _sources.JoinToString(';'));

                private readonly string[] _sources;

                public MultiServerSourceTester(string[] sources) {
                    _sources = sources;
                }

                public bool Test(ServerEntry obj) {
                    return obj.OriginsFrom(_sources);
                }

                public bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.Origins);
                }

                public override string ToString() {
                    return @"[" + Source + @"]";
                }
            }

            [NotNull]
            private static CombinedFilter<ServerEntry> GetFilter([CanBeNull] string filterString, [CanBeNull] string[] sources) {
                IFilter<ServerEntry> sourcesTester;
                if (sources == null || sources.Length == 0) {
                    sourcesTester = new ServerSourceTester(KunosOnlineSource.Key);
                } else if (sources.Contains(@"*")) {
                    sourcesTester = null;
                } else if (sources.Length == 1) {
                    sourcesTester = new ServerSourceTester(sources[0]);
                } else {
                    sourcesTester = new MultiServerSourceTester(sources);
                }

                if (filterString == null) {
                    return new CombinedFilter<ServerEntry>(sourcesTester);
                }

                var filter = Filter.Create(ServerEntryTester.Instance, filterString);
                if (sourcesTester == null) return new CombinedFilter<ServerEntry>(filter);

                return new CombinedFilter<ServerEntry>(new CombinedFilter<ServerEntry>(filter) {
                    Second = sourcesTester
                });
            }

            public OnlineViewModel([CanBeNull] string filterParam) {
                string[] sources = null;
                string filter = null;

                if (filterParam != null) {
                    var splitIndex = filterParam.LastIndexOf('@');
                    if (splitIndex != -1) {
                        sources = filterParam.Substring(splitIndex + 1).Split(',').Select(x => x.Trim().ToLowerInvariant())
                                              .Distinct().ToArray();
                        filter = splitIndex == 0 ? null : filterParam.Substring(0, splitIndex);
                    } else {
                        filter = filterParam;
                    }
                }

                Pack = OnlineManager.Instance.GetSourcesPack(sources);

                ListFilter = GetFilter(filter, sources);
                Key = GetKey(filterParam);
                
                Manager = OnlineManager.Instance;
                MainList = new BetterListCollectionView(Manager.List) {
                    IsLiveFiltering = true,
                    IsLiveSorting = true
                };

                LoadQuickFilter();
                SortingMode = SortingModes.GetByIdOrDefault(LimitedStorage.Get(LimitedSpace.OnlineSorting, Key)) ?? SortingModes[0];
                ListFilter.Second = CreateQuickFilter();
            }

            private bool _loaded;

            public void Load() {
                if (_loaded) return;
                _loaded = true;
                
                Manager.List.ItemPropertyChanged += List_ItemPropertyChanged;

                using (MainList.DeferRefresh()) {
                    MainList.Filter = FilterTest;
                    MainList.CustomSort = Sorting;
                }

                LoadCurrent();
                MainList.CurrentChanged += OnCurrentChanged;

                if (Pack.Status == OnlineManagerStatus.Ready) {
                    StartPinging().Forget();
                } else {
                    StartLoading().Forget();
                }

                Pack.Ready += Pack_Ready;
            }

            public void Unload() {
                if (!_loaded) return;
                _loaded = false;

                Manager.List.ItemPropertyChanged -= List_ItemPropertyChanged;
                StopLoading();
                StopPinging();

                Pack.Ready -= Pack_Ready;
            }

            private void Pack_Ready(object sender, EventArgs e) {
                Logging.Here();
                StartPinging().Forget();
            }

            private CancellationTokenSource _currentLoading;

            private async Task StartLoading() {
                var cancellation = new CancellationTokenSource();
                try {
                    _currentLoading = cancellation;
                    await Pack.EnsureLoadedAsync(cancellation.Token);
                } finally {
                    if (Equals(_currentLoading, cancellation)) {
                        _currentLoading = null;
                    }

                    cancellation.Dispose();
                }
            }

            private void StopLoading() {
                if (_currentLoading != null) {
                    _currentLoading.Cancel();
                    _currentLoading = null;
                }
            }

            private CancellationTokenSource _currentPinging;

            private async Task StartPinging() {
                // Just a little delay to make sure sources pack is loaded
                await Task.Delay(TimeSpan.FromMilliseconds(100));

                StopLoading();
                StopPinging();

                var cancellation = new CancellationTokenSource();
                try {
                    _currentPinging = cancellation;
                    await Manager.PingEverything(ListFilter, cancellation.Token);
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

            private AsyncCommand<bool?> _refreshCommand;

            public AsyncCommand<bool?> RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand<bool?>(async nonReadyOnly => {
                // StopPinging();
                // why pinging doesn’t crash when collection is getting updated?
                await Pack.ReloadAsync(nonReadyOnly ?? false);
            }));

            protected void OnCurrentChanged(object sender, EventArgs e) {
                // base.OnCurrentChanged(sender, e);
                CurrentChanged();
            }

            private void CurrentChanged() {
                var currentId = ((ServerEntry)MainList.CurrentItem)?.Id;
                if (currentId == null) return;

                SelectedSource = UriExtension.Create("/Pages/Drive/Online_SelectedServerPage.xaml?Id={0}", currentId);
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
