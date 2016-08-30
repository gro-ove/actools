﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Annotations;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;
using Prompt = AcManager.Controls.Dialogs.Prompt;
using WaitingDialog = AcManager.Controls.Dialogs.WaitingDialog;

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
            if (_manager.IsScanned) return;
            await _manager.ScanAsync();
        }

        public void Load() {
            if (_manager.IsScanned) return;
            _manager.Scan();
        }

        public void Initialize() {
            DataContext = new OnlineViewModel(_type, _manager, _filter);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.Manager.RefreshListCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(Model.AddNewServerCommand, new KeyGesture(Key.A, ModifierKeys.Control))
            });
            InitializeComponent();

            ResizingStuff();
        }

        private DispatcherTimer _timer;

        private OnlineViewModel Model => (OnlineViewModel)DataContext;

        private static bool IsUserVisible(FrameworkElement element, FrameworkElement container) {
            if (element.IsVisible != true) return false;
            var bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            var rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
        }

        private IEnumerable<T> GetVisibleItemsFromListbox<T>(ItemsControl listBox) {
            var any = false;
            foreach (var item in listBox.Items.OfType<T>()) {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item);
                if (container != null && IsUserVisible((ListBoxItem)container, ServersListBox)) {
                    any = true;
                    yield return item;
                } else if (any) {
                    break;
                }
            }
        }

        private int _timerTick;

        private void Timer_Tick(object sender, EventArgs e) {
            if (++_timerTick > 5) {
                _timerTick = 0;

                if (Model.ServerCombinedFilter?.First?.IsAffectedBy(nameof(ServerEntry.SessionEnd)) == true && Model.Manager != null) {
                    foreach (var serverEntry in Model.Manager.LoadedOnly) {
                        serverEntry.OnSessionEndTick();
                    }
                }
            }

            if (SimpleListMode != ListMode.DetailedPlus) return;
            foreach (var item in GetVisibleItemsFromListbox<AcItemWrapper>(ServersListBox)) {
                (item.Value as ServerEntry)?.OnTick();
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

        private void Online_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        private bool _loaded;
        private TaskbarProgress _taskbarProgress;

        private void Online_OnLoaded(object sender, RoutedEventArgs e) {
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
        }

        private void Online_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            Model.Manager.PropertyChanged -= Manager_PropertyChanged;
            Model.Unload();

            _taskbarProgress?.Dispose();
            _timer?.Stop();
        }

        private void Manager_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
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
                return $"<{First}>+<{Second}>";
            }
        }

        public class OnlineViewModel : AcObjectListCollectionViewWrapper<ServerEntry> {
            public OnlineManagerType Type { get; set; }

            public BaseOnlineManager Manager { get; }

            public bool ShowList => MainList.Count > 0 || SelectedSource != null;

            protected override void FilteredNumberChanged(int oldValue, int newValue) {
                if (oldValue == 0 || newValue == 0) {
                    OnPropertyChanged(nameof(ShowList));
                }
            }

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
                if (!Loaded || _updatingQuickFilter) return;
                _updatingQuickFilter = true;

                await Task.Delay(50);
                SaveQuickFilter();
                ServerCombinedFilter.Second = CreateQuickFilter();
                MainList.Refresh();

                if (CurrentItem == null) {
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

            public OnlineViewModel(OnlineManagerType type, BaseOnlineManager manager, string filter)
                    : base(manager, GetFilter(filter), type.ToString(), false) {
                Type = type;
                Manager = manager;

                LoadQuickFilter();
                SortingMode = SortingModes.GetByIdOrDefault(LimitedStorage.Get(LimitedSpace.OnlineSorting, Key)) ?? SortingModes[0];
                ServerCombinedFilter.Second = CreateQuickFilter();
            }

            private CancellationTokenSource _pingingSource;

            public override void Load() {
                if (Loaded) return;

                base.Load();
                CurrentChanged();

                _pingingSource = new CancellationTokenSource();
                Manager.PingEverything(ListFilter, _pingingSource.Token).Forget();
            }

            public override void Unload() {
                if (_pingingSource != null) {
                    _pingingSource.Cancel();
                    _pingingSource.Dispose();
                    _pingingSource = null;
                }

                base.Unload();
            }

            private class SortingDriversCount : AcObjectSorter<ServerEntry> {
                public override int Compare(ServerEntry x, ServerEntry y) {
                    var dif = -x.CurrentDriversCount.CompareTo(y.CurrentDriversCount);
                    return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
                }

                public override bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.CurrentDriversCount);
                }
            }

            private class SortingCapacityCount : AcObjectSorter<ServerEntry> {
                public override int Compare(ServerEntry x, ServerEntry y) {
                    var dif = -x.Capacity.CompareTo(y.Capacity);
                    return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
                }

                public override bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.Capacity);
                }
            }

            private class SortingCarsNumberCount : AcObjectSorter<ServerEntry> {
                public override int Compare(ServerEntry x, ServerEntry y) {
                    var dif = -x.CarIds.Length.CompareTo(y.CarIds.Length);
                    return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
                }

                public override bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.CarIds);
                }
            }

            private class SortingPing : AcObjectSorter<ServerEntry> {
                public override int Compare(ServerEntry x, ServerEntry y) {
                    const long maxPing = 999999;
                    var dif = (x.Ping ?? maxPing).CompareTo(y.Ping ?? maxPing);
                    return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
                }

                public override bool IsAffectedBy(string propertyName) {
                    return propertyName == nameof(ServerEntry.Ping);
                }
            }

            private SettingEntry _sortingMode;

            public SettingEntry SortingMode {
                get { return _sortingMode; }
                set {
                    if (!SortingModes.Contains(value)) value = SortingModes[0];
                    if (Equals(value, _sortingMode)) return;

                    switch (value.Value) {
                        case "drivers":
                            Sorting = new SortingDriversCount();
                            break;

                        case "capacity":
                            Sorting = new SortingCapacityCount();
                            break;

                        case "cars":
                            Sorting = new SortingCarsNumberCount();
                            break;

                        case "ping":
                            Sorting = new SortingPing();
                            break;

                        default:
                            Sorting = null;
                            break;
                    }

                    _sortingMode = value;
                    OnPropertyChanged();
                    LimitedStorage.Set(LimitedSpace.OnlineSorting, Key, _sortingMode.Id);
                }
            }

            public SettingEntry[] SortingModes { get; } = {
                new SettingEntry(null, AppStrings.Online_Sorting_Name),
                new SettingEntry("drivers", AppStrings.Online_Sorting_Drivers),
                new SettingEntry("capacity", AppStrings.Online_Sorting_Capacity),
                new SettingEntry("cars", AppStrings.Online_Sorting_CarsNumber),
                new SettingEntry("ping", AppStrings.Online_Sorting_Ping),
            };

            private RelayCommand _changeSortingCommand;

            public RelayCommand ChangeSortingCommand => _changeSortingCommand ?? (_changeSortingCommand = new RelayCommand(o => {
                SortingMode = SortingModes.GetByIdOrDefault(o as string) ?? SortingModes[0];
            }));

            private AsyncCommand _addNewServerCommand;

            public AsyncCommand AddNewServerCommand => _addNewServerCommand ?? (_addNewServerCommand = new AsyncCommand(async o => {
                var address = Prompt.Show(AppStrings.Online_AddServer, AppStrings.Online_AddServer_Title, "", // TODO
                        @"127.0.0.1:8081", AppStrings.Online_AddServer_Tooltip);
                if (address == null) return;

                foreach (var s in address.Split(',').Select(x => x.Trim())) {
                    try {
                        using (var waiting = new WaitingDialog()) {
                            waiting.Report(AppStrings.Online_AddingServer);
                            await RecentManager.Instance.AddServer(s, waiting, waiting.CancellationToken);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify(AppStrings.Online_CannotAddServer, e);
                    }
                }
            }, o => Manager is RecentManager));

            protected override void OnCurrentChanged(object sender, EventArgs e) {
                base.OnCurrentChanged(sender, e);
                CurrentChanged();
            }

            private void CurrentChanged() {
                var currentId = ((AcItemWrapper)MainList.CurrentItem)?.Value.Id;
                if (currentId == null) return;

                SelectedSource = UriExtension.Create("/Pages/Drive/Online_SelectedServerPage.xaml?Mode={0}&Id={1}", Type, currentId);
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
    }
}
