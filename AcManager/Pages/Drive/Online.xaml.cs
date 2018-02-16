using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.DiscordRpc;
using AcManager.Pages.Dialogs;
using JetBrains.Annotations;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using StringBasedFilter;
using Prompt = FirstFloor.ModernUI.Dialogs.Prompt;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.Pages.Drive {
    public partial class Online : IParametrizedUriContent, IContent {
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(50, "Preparing to race", "Online");

        private string[] _hideIconSourceIds;

        private void SetHideIcon() {
            var sources = Model.Pack.SourceWrappers;

            var hideIcons = new List<string>();
            if (sources.Count == 1) {
                hideIcons.Add(sources[0].Id);
            }

            if (sources.All(x => x.Id != MinoratingOnlineSource.Key)) {
                // As @Topuz pointed out, same inputs shouldn’t provide different output. So,
                // if user visited a list of servers without Minorating details loaded, then switched
                // to Minorating tab, and then back, they shouldn’t see those icons suddenly appeared.
                hideIcons.Add(MinoratingOnlineSource.Key);
            }

            _hideIconSourceIds = hideIcons.ToArray();
        }

        public void OnUri(Uri uri) {
            this.OnActualUnload(_discordPresence);

            DataContext = new OnlineViewModel(uri.GetQueryParam("Filter"), ShowDetails);
            SetHideIcon();

            InputBindings.AddRange(new[] {
                new InputBinding(Model.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(Model.AddNewServerCommand, new KeyGesture(Key.A, ModifierKeys.Control))
            });

            InitializeComponent();
            ResizingStuff();

            var pack = Model.Pack;
            pack.SubscribeWeak(OnPackPropertyChanged);
            this.OnActualUnload(() => pack.UnsubscribeWeak(OnPackPropertyChanged));

            BigButtonsParent.AddWidthCondition(430).Add(FilteringComboBox);
        }

        private async void OnPackPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SourcesPack.Status) && Model.Pack?.Status == OnlineManagerStatus.Ready) {
                await Task.Delay(1);
                ResizingStuff();
            }
        }

        private void ShowDetails(ServerEntry entry) {
            Model.ServerSelected = true;
            Frame.Source = UriExtension.Create("/Pages/Drive/OnlineServer.xaml?Id={0}", entry.Id);
        }

        private DispatcherTimer _timer;

        private OnlineViewModel Model => (OnlineViewModel)DataContext;

        private int _timerTick;

        private void OnTimerTick(object sender, EventArgs e) {
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
            Simple,
            Detailed,
            DetailedPlus
        }

        private ListMode? _simpleListMode;

        public ListMode SimpleListMode {
            get => _simpleListMode ?? ListMode.Simple;
            set {
                if (Equals(value, _simpleListMode)) return;

                if (value == ListMode.Simple) {
                    ServersListBox.ItemTemplate = (DataTemplate)FindResource(@"SimpleListItem");
                } else {
                    var factory = new FrameworkElementFactory(typeof(OnlineItem));
                    factory.SetBinding(OnlineItem.ServerProperty, new Binding());

                    if (value == ListMode.DetailedPlus) {
                        factory.SetValue(StyleProperty, FindResource(@"OnlineItem.Plus"));
                    }

                    factory.SetValue(OnlineItem.HideSourceIconsProperty, _hideIconSourceIds);

                    ServersListBox.ItemTemplate = new DataTemplate(typeof(OnlineItem)) {
                        VisualTree = factory
                    };
                }

                if (value == ListMode.Simple || _simpleListMode == ListMode.Simple) {
                    ServersListBox.ItemContainerStyle =
                            (Style)FindResource(value == ListMode.Simple ? @"SimpledListItemContainer" : @"DetailedListItemContainer");
                }

                _simpleListMode = value;
            }
        }

        private bool? _wideInformationMode;

        public bool WideInformationMode {
            get => _wideInformationMode ?? false;
            set {
                if (Equals(value, _wideInformationMode)) return;
                _wideInformationMode = value;
                Frame.Width = value ? 670 : 450;
            }
        }


        private void ResizingStuff() {
            var width = ActualWidth;
            SimpleListMode = width > 1080 ? ListMode.DetailedPlus : width > 800 ? ListMode.Detailed : ListMode.Simple;
            WideInformationMode = width > 1280;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        private TaskbarHolder _taskbarProgress;
        private bool _scrolling;

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            foreach (var child in ServersListBox.FindVisualChildren<OnlineItem>()) {
                child.SetScrolling(_scrolling);
            }
        }

        private void OnScrollStarted(object sender, DragStartedEventArgs e) {
            _scrolling = true;
            foreach (var child in ServersListBox.FindVisualChildren<OnlineItem>()) {
                child.SetScrolling(true);
            }
        }

        private void OnScrollCompleted(object sender, DragCompletedEventArgs e) {
            _scrolling = false;
            foreach (var child in ServersListBox.FindVisualChildren<OnlineItem>()) {
                child.SetScrolling(false);
            }
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
            return value;
        }

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.OnlineQuickFilter, GetKey(e.OldValue), GetKey(e.NewValue));
            LimitedStorage.Move(LimitedSpace.OnlineSelected, GetKey(e.OldValue), GetKey(e.NewValue));
            LimitedStorage.Move(LimitedSpace.OnlineSorting, GetKey(e.OldValue), GetKey(e.NewValue));
        }

        public static async Task AddServerManually(string listKey) {
            var address = Prompt.Show(AppStrings.Online_AddServer, AppStrings.Online_AddServer_Title, "",
                    @"127.0.0.1:8081", AppStrings.Online_AddServer_Tooltip);
            if (address == null) return;

            try {
                var list = new List<ServerInformationComplete>();
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(AppStrings.Online_AddingServer);

                    foreach (var s in address.Split(',').Select(x => x.Trim())) {
                        var part = await OnlineManager.Instance.ScanForServers(s, waiting, waiting.CancellationToken);
                        if (part == null || waiting.CancellationToken.IsCancellationRequested) return;

                        list.AddRange(part);
                    }
                }

                if (list.Count == 0) {
                    ModernDialog.ShowMessage("Nothing found.", AppStrings.Online_AddServer_Title, MessageBoxButton.OK);
                } else {
                    new OnlineAddManuallyDialog(listKey, list).ShowDialog();
                }
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Online_CannotAddServer, e);
            }
        }

        public class OnlineViewModel : NotifyPropertyChanged {
            public string Key { get; }
            public OnlineManager Manager { get; }
            public SourcesPack Pack { get; }
            public BetterListCollectionView MainList { get; }

            [NotNull]
            public readonly CombinedFilter<ServerEntry> ListFilter;

            private bool _updatingQuickFilter;

            private async void UpdateQuickFilter() {
                if (!_loaded || _updatingQuickFilter) return;
                _updatingQuickFilter = true;

                await Task.Delay(50);
                ListFilter.Second = Filters.CreateFilter();
                MainList.Refresh();

                if (MainList.CurrentItem == null) {
                    MainList.MoveCurrentToFirst();
                }

                _updatingQuickFilter = false;
            }

            private bool _serverSelected;

            public bool ServerSelected {
                get => _serverSelected;
                set => Apply(value, ref _serverSelected);
            }

            private class ServerSourceTester : IFilter<ServerEntry> {
                public string Source => _source;

                private readonly string _source;

                public ServerSourceTester(string source) {
                    _source = source;
                }

                public bool Test(ServerEntry obj) {
                    return obj.ReferencedFrom(_source) && (_source == FileBasedOnlineSources.HiddenKey || !obj.IsExcluded);
                }

                public bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.ReferencesString)
                            || _source != FileBasedOnlineSources.HiddenKey && propertyName == nameof(ServerEntry.IsExcluded);
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
                    return obj.ReferencedFrom(_sources) && !obj.IsExcluded;
                }

                public bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.ReferencesString)
                            || propertyName == nameof(ServerEntry.IsExcluded);
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
                } else if (sources.ArrayContains(@"*") || sources.ArrayContains(@"all")) {
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

            public bool UserListMode { get; }

            public SettingEntry[] SortingModes { get; }
            public OnlineQuickFilters Filters { get; }

            private readonly Action<ServerEntry> _showDetails;

            public OnlineViewModel([CanBeNull] string filterParam, Action<ServerEntry> showDetails) {
                _showDetails = showDetails;

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
                UserListMode = Pack.SourceWrappers.Count == 1 && FileBasedOnlineSources.Instance.GetUserSources()
                                                                                       .Select(x => x.Id)
                                                                                       .Append(FileBasedOnlineSources.FavouritesKey)
                                                                                       .Contains(Pack.SourceWrappers[0].Id);

                ListFilter = GetFilter(filter, sources);
                Key = GetKey(filterParam);

                Manager = OnlineManager.Instance;
                MainList = new BetterListCollectionView(Manager.List);

                SortingModes = sources?.Length == 1 && sources[0] == FileBasedOnlineSources.RecentKey
                        ? DefaultSortingModes.Prepend(new SettingEntry(null, "Default")).ToArray() : DefaultSortingModes;

                SortingMode = SortingModes.GetByIdOrDefault(LimitedStorage.Get(LimitedSpace.OnlineSorting, Key)) ?? SortingModes[0];
                // ListFilter.Second = CreateQuickFilter();

                Filters = new OnlineQuickFilters(Key);
                ListFilter.Second = Filters.CreateFilter();
                Filters.Changed += OnFiltersChanged;
            }

            private void OnFiltersChanged(object sender, EventArgs eventArgs) {
                UpdateQuickFilter();
            }

            private bool _loaded;

            public void Load() {
                Debug.Assert(!_loaded);
                _loaded = true;

                Manager.List.CollectionChanged += List_CollectionChanged;
                Manager.List.ItemPropertyChanged += List_ItemPropertyChanged;

                using (MainList.DeferRefresh()) {
                    MainList.Filter = FilterTest;
                    MainList.CustomSort = Sorting;
                }

                if (!Pack.LoadingComplete) {
                    // In case pack is basically ready, but there is still something loads in background,
                    // we need to use StartLoading() anyway — current loading process will be cancelled,
                    // if there is no “listeners”
                    StartLoading().Forget();
                }

                if (Pack.Status == OnlineManagerStatus.Ready) {
                    StartPinging().Forget();
                }

                LoadCurrent();
                MainList.CurrentChanged += OnCurrentChanged;

                Pack.Ready += Pack_Ready;
            }

            public void Unload() {
                Debug.Assert(_loaded);
                _loaded = false;

                Manager.List.CollectionChanged -= List_CollectionChanged;
                Manager.List.ItemPropertyChanged -= List_ItemPropertyChanged;

                StopLoading();
                StopPinging();

                Pack.Ready -= Pack_Ready;
                Pack.Dispose();

                Filters.Dispose();
            }

            private void Pack_Ready(object sender, EventArgs e) {
                StartPinging().Forget();
                LoadCurrent();
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

            private string _loadCurrentFailed;
            private bool _currentLoaded;

            private void LoadCurrent() {
                if (_currentLoaded) {
                    if (_loadCurrentFailed != null) {
                        var selected = Manager.GetById(_loadCurrentFailed);
                        if (selected != null) {
                            MainList.MoveCurrentTo(selected);
                            _loadCurrentFailed = null;
                        }
                    }
                    return;
                }

                _currentLoaded = true;
                MainList.CurrentChanged -= OnCurrentChanged;

                var current = LimitedStorage.Get(LimitedSpace.OnlineSelected, Key);
                if (current != null) {
                    var selected = Manager.GetById(current);
                    if (selected != null) {
                        MainList.MoveCurrentTo(selected);
                        _loadCurrentFailed = null;
                    } else {
                        MainList.MoveCurrentToFirst();
                        _loadCurrentFailed = current;
                    }
                } else {
                    MainList.MoveCurrentToFirst();
                }

                CurrentChanged(false);
                MainList.CurrentChanged += OnCurrentChanged;
            }

            private object _testMeLater;

            private void List_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset) {
                    LoadCurrent();
                }
            }

            private void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (!_loaded) return;

                if (ListFilter.IsAffectedBy(e.PropertyName)) {
                    var server = (ServerEntry)sender;
                    var willBeVisible = ListFilter.Test(server);

                    if (ReferenceEquals(_current, server)) {
                        _testMeLater = willBeVisible ? null : server;
                        return;
                    }

                    var wasVisible = MainList.Contains(sender);
                    if (willBeVisible != wasVisible) {
                        MainList.Refresh(sender);
                        return;
                    }
                }

                if (e.PropertyName == nameof(ServerEntry.IsFavourite) || e.PropertyName == nameof(ServerEntry.ReferencesString)) {
                    return;
                }

                if (Sorting?.IsAffectedBy(e.PropertyName) == true) {
                    MainList.Refresh(sender);
                }
            }

            private bool FilterTest(object obj) {
                if (ReferenceEquals(_testMeLater, obj)) return true;
                return obj is ServerEntry s && ListFilter.Test(s);
            }

            private ServerEntrySorter _sorting;

            [CanBeNull]
            public ServerEntrySorter Sorting {
                get => _sorting;
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
                get => _sortingMode;
                set {
                    if (!SortingModes.ArrayContains(value)) value = SortingModes[0];
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

            public ICommand AddNewServerCommand => _addNewServerCommand ??
                    (_addNewServerCommand = UserListMode ? new AsyncCommand(() => AddServerManually(Pack.SourceWrappers[0].Id)) : UnavailableCommand.Instance);

            private AsyncCommand<bool?> _refreshCommand;

            public AsyncCommand<bool?> RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand<bool?>(async nonReadyOnly => {
                // StopPinging();
                // why pinging doesn’t crash when collection is getting updated?
                await Pack.ReloadAsync(nonReadyOnly ?? false);
            }));

            protected void OnCurrentChanged(object sender, EventArgs e) {
                // base.OnCurrentChanged(sender, e);
                CurrentChanged(true);
            }

            private ServerEntry _current;

            private void CurrentChanged(bool save) {
                var item = (ServerEntry)MainList.CurrentItem;
                if (item == null) return;

                if (save) {
                    LimitedStorage.Set(LimitedSpace.OnlineSelected, Key, item.Id);
                    _loadCurrentFailed = null;
                }

                if (ReferenceEquals(_current, item)) return;
                _current = item;
                _showDetails.Invoke(item);

                if (_testMeLater != null) {
                    var testMeLater = _testMeLater;
                    _testMeLater = null;
                    MainList.Refresh(testMeLater);
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

        public void OnFragmentNavigation(FragmentNavigationEventArgs e) {}

        public void OnNavigatedFrom(NavigationEventArgs e) {
            Model.Unload();

            _taskbarProgress?.Dispose();
            _timer?.Stop();
        }

        public void OnNavigatedTo(NavigationEventArgs e) {
            Model.Load();

            _taskbarProgress = TaskbarService.Create(1000d, () => {
                switch (Model.Pack.Status) {
                    case OnlineManagerStatus.Ready:
                        return Tuple.Create(TaskbarState.NoProgress, 1d);
                    case OnlineManagerStatus.Error:
                        return Tuple.Create(TaskbarState.Error, 1d);
                    case OnlineManagerStatus.Waiting:
                        return Tuple.Create(TaskbarState.Indeterminate, 0d);
                    default:
                        var v = Model.Pack.SourceWrappers;
                        var m = v.Select(x => x.LoadingProgress).Where(x => !x.IsIndeterminate && !x.IsReady).MinEntryOrDefault(x => x.Progress ?? 0d).Progress;
                        return m > 0 ? Tuple.Create(TaskbarState.Normal, m.Value) : Tuple.Create(TaskbarState.Indeterminate, 0d);
                }
            });

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            _timer.Tick += OnTimerTick;

            var scrollViewer = ServersListBox.FindVisualChild<ScrollViewer>();
            var viewer = scrollViewer?.FindVisualChild<Thumb>();

            if (viewer != null) {
                scrollViewer.ScrollChanged += OnScrollChanged;
                viewer.DragStarted += OnScrollStarted;
                viewer.DragCompleted += OnScrollCompleted;
            }
        }

        public void OnNavigatingFrom(NavigatingCancelEventArgs e) {}
    }
}
