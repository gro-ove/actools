using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.DiscordRpc;
using AcManager.Pages.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class SpecialEvents : ILoadableContent, IParametrizedUriContent {
        public static bool OptionScalableTiles = true;
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(10, "Preparing to race", "Challenges");

        private string _filter;

        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return SpecialEventsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            SpecialEventsManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            this.OnActualUnload(_discordPresence);
            DataContext = new ViewModel(_filter, _discordPresence);
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(() => Model.Selected?.GoCommand.Execute(null)), new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => Model.Selected?.ViewInExplorerCommand.Execute(null)), new KeyGesture(Key.F, ModifierKeys.Control))
            });

            this.AddSizeCondition(c => c.ActualHeight > 640).Add(v => {
                BestLapBlock.Margin = v ? new Thickness(0, 16, 0, 0) : new Thickness();
            });

            /*Dispatcher.InvokeAsync(async () => {
                await Task.Delay(1000);
                if (Model.Selected != null) {
                    ListBox.ScrollIntoView(ListBox.SelectedItem);
                }
            });*/
        }

        private ViewModel Model => (ViewModel)DataContext;

        private class ViewModel : NotifyPropertyChanged, IComparer {
            private readonly DiscordRichPresence _discordPresence;

            public AcWrapperCollectionView List { get; }

            private SpecialEventObject _selected;

            [CanBeNull]
            public SpecialEventObject Selected {
                get => _selected;
                set {
                    if (Equals(value, _selected)) return;
                    _selected = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeySelectedId, value?.Id);
                    // _discordPresence.LargeImage = value == null ? null : new DiscordImage("", value.DisplayName);
                    _discordPresence.Details = value == null ? "Challenges" : $"Challenges | {value.DisplayName}";
                }
            }

            public ViewModel(string filter, DiscordRichPresence discordPresence) {
                _discordPresence = discordPresence;
                List = new AcWrapperCollectionView(SpecialEventsManager.Instance.WrappersAsIList);
                List.CurrentChanged += OnCurrentChanged;
                List.MoveCurrentToIdOrFirst(ValuesStorage.GetString(KeySelectedId));

                if (!string.IsNullOrWhiteSpace(filter)) {
                    var filterObject = Filter.Create(SpecialEventObjectTester.Instance, filter);
                    List.Filter = wrapper => {
                        var v = (wrapper as AcItemWrapper)?.Value as SpecialEventObject;
                        return v != null && filterObject.Test(v);
                    };
                }

                List.CustomSort = this;

                if (_selectNext != null) {
                    Selected = _selectNext;
                    _selectNext = null;
                }
            }

            private void OnCurrentChanged(object sender, EventArgs e) {
                Selected = List.LoadedCurrent as SpecialEventObject;
                if (Selected != null) {
                    FancyBackgroundManager.Instance.ChangeBackground(Selected.PreviewImage);
                }
            }

            public void Unload() { }

            int IComparer.Compare(object x, object y) {
                return AlphanumComparatorFast.Compare((x as AcItemWrapper)?.Id, (y as AcItemWrapper)?.Id);
            }

            private ICommand _updateProgressCommand;

            public ICommand SyncronizeProgressCommand => _updateProgressCommand ??
                    (_updateProgressCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgress(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SteamIdHelper.Instance.Value != null));

            private ICommand _syncronizeProgressUsingModuleCommand;

            public ICommand SyncronizeProgressUsingModuleCommand => _syncronizeProgressUsingModuleCommand ??
                    (_syncronizeProgressUsingModuleCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgressViaModule(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.UiModuleStarterType));

            private ICommand _syncronizeProgressUsingSidePassageCommand;

            public ICommand SyncronizeProgressUsingSidePassageCommand => _syncronizeProgressUsingSidePassageCommand ??
                    (_syncronizeProgressUsingSidePassageCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgressViaSidePassage(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.SidePassageStarterType));

            private AsyncCommand _syncronizeProgressUsingSteamStarterCommand;

            public AsyncCommand SyncronizeProgressUsingSteamStarterCommand => _syncronizeProgressUsingSteamStarterCommand ??
                    (_syncronizeProgressUsingSteamStarterCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgressViaSteamStarter(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.SteamStarterType));

            private AsyncCommand _syncronizeProgressUsingAppIdStarterCommand;

            public AsyncCommand SyncronizeProgressUsingAppIdStarterCommand => _syncronizeProgressUsingAppIdStarterCommand ??
                    (_syncronizeProgressUsingAppIdStarterCommand = new AsyncCommand(async () => {
                        try {
                            using (var waiting = new WaitingDialog()) {
                                await SpecialEventsManager.Instance.UpdateProgressViaAppIdStarter(waiting, waiting.CancellationToken);
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t get challenges progress", e);
                        }
                    }, () => SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.AppIdStarterType));
        }

        private ScrollViewer _scroll;

        private const string KeyScrollValue = ".SpecialEvents.Scroll";
        private const string KeySelectedId = ".SpecialEvents.Selected";

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _scroll = ListBox.FindVisualChild<ScrollViewer>();
            if (_scroll != null) {
                _scroll.LayoutUpdated += ScrollLayoutUpdated;
            }
        }

        private bool _positionLoaded;

        private async void ScrollLayoutUpdated(object sender, EventArgs e) {
            if (_positionLoaded) return;
            var value = ValuesStorage.GetDoubleNullable(KeyScrollValue) ?? 0d;
            await Task.Delay(10);
            _scroll?.ScrollToHorizontalOffset(OptionScalableTiles ? value / (Equals(_scale, 0d) ? 1d : _scale) : value);
            _positionLoaded = true;
        }

        private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) {}

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scroll == null || !_positionLoaded) return;
            ValuesStorage.Set(KeyScrollValue, OptionScalableTiles ? _scroll.HorizontalOffset * _scale : _scroll.HorizontalOffset);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private double _scale;

        private void ResizeTiles() {
            if (_tilePanel == null || !OptionScalableTiles) return;

            _positionLoaded = false;

            // Width="195" Height="110"
            var scale = 1d + ((ActualHeight - 640d) / 540d).Saturate();
            if (scale < 1.3) scale = 1d;
            if (Math.Abs(scale - _scale) < 0.2) return;

            _tilePanel.ItemWidth = 195d * scale;
            _tilePanel.ItemHeight = 110d * scale;
            _scale = scale;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizeTiles();
        }

        private VirtualizingTilePanel _tilePanel;

        private void TilePanel_OnLoaded(object sender, RoutedEventArgs e) {
            _tilePanel = (VirtualizingTilePanel)sender;
            ResizeTiles();
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Drive/SpecialEvents.xaml", UriKind.Relative));
        }

        private static SpecialEventObject _selectNext;

        public static void Show(SpecialEventObject select) {
            _selectNext = select;
            NavigateToPage();
        }

        private void OnCarPreviewClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled || Model.Selected == null) return;
            e.Handled = true;
            new ImageViewer(
                    Model.Selected.CarSkin.PreviewImage,
                    CommonAcConsts.PreviewWidth,
                    details: CarBlock.GetSkinImageViewerDetailsCallback(Model.Selected.CarObject)).ShowDialog();
        }
    }
}
