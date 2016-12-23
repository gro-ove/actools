using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using CarEntry = AcManager.Tools.Managers.Online.ServerEntry.CarEntry;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer : IParametrizedUriContent, IImmediateContent {
        private Holder<ServerEntry> _holder;
        private Sublist<MenuItem> _sourcesMenuItems;

        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            _holder = OnlineManager.Instance.HoldById(id);
            if (_holder == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }

            var entry = _holder.Value;
            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Forget();
            }

            var list = ListsButton.MenuItems;
            _sourcesMenuItems = new Sublist<MenuItem>(list, list.Count - 1, 0);

            DataContext = new ViewModel(entry);
            InitializeComponent();
            UpdateCarsView();
            UpdateIcons();
            UpdateBindings();
            Model.Entry.PropertyChanged += Entry_PropertyChanged;
            ResizingStuff();
        }

        private bool _showExtendedInformation;

        public bool ShowExtendedInformation {
            get { return _showExtendedInformation; }
            set {
                if (Equals(value, _showExtendedInformation)) return;
                _showExtendedInformation = value;

                if (value) {
                    ExtendedInformation.Visibility = Visibility.Visible;
                    CarsListBox.ItemsSource = CarsComboBox.ItemsSource;
                    CarsListBox.SelectedItem = CarsComboBox.SelectedItem;
                } else {
                    ExtendedInformation.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ResizingStuff() {
            ShowExtendedInformation = ActualWidth > 600;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        private void OnUserSourcesUpdated(object sender, EventArgs e) {
            UpdateMenuItems();
        }

        private bool _listsButtonInitialized;

        private void ListsButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (_listsButtonInitialized) return;
            _listsButtonInitialized = true;

            UpdateMenuItems();
            FileBasedOnlineSources.Instance.Update += OnUserSourcesUpdated;
        }

        public bool ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var existing = _holder;
            _holder = OnlineManager.Instance.HoldById(id);
            DisposeHelper.Dispose(ref existing);
            if (_holder == null) return false;

            var entry = _holder.Value;
            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Forget();
            }

            Model.Entry.PropertyChanged -= Entry_PropertyChanged;
            Model.ChangeEntry(entry);
            UpdateCarsView();
            UpdateIcons();
            UpdateBindings();
            UpdateCheckedMenuItems();
            Model.Entry.PropertyChanged += Entry_PropertyChanged;
            return true;
        }

        private void UpdateBindings() {
            InputBindings.Clear();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.Entry.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.Entry.ToggleFavouritedCommand, new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(Model.Entry.ToggleHiddenCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.Entry.CancelBookingCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.ManageListsCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)) {
                    CommandParameter = ServerEntry.ForceJoin
                }
            });
            InputBindingBehavior.UpdateBindings(this);
        }

        private class MenuComparer : IComparer<MenuItem> {
            public static readonly IComparer<MenuItem> Instance = new MenuComparer();

            public int Compare(MenuItem x, MenuItem y) {
                return string.Compare(x.Header.ToString(), y.Header.ToString(), StringComparison.CurrentCulture);
            }
        }

        private DelegateCommand<string> _toggleSourceCommand;

        public DelegateCommand<string> ToggleSourceCommand => _toggleSourceCommand ?? (_toggleSourceCommand = new DelegateCommand<string>(key => {
            var entry = Model.Entry;
            if (entry.ReferencedFrom(key)) {
                FileBasedOnlineSources.RemoveFromList(key, entry);
            } else {
                FileBasedOnlineSources.AddToList(key, entry);
            }
        }));

        private void UpdateMenuItems() {
            var entry = Model.Entry;
            var sources = FileBasedOnlineSources.Instance.GetUserSources().ToList();

            for (var i = _sourcesMenuItems.Count - 1; i >= 0; i--) {
                var id = _sourcesMenuItems[i].Tag as string;
                if (sources.GetByIdOrDefault(id) == null) {
                    _sourcesMenuItems.RemoveAt(i);
                }
            }

            _sourcesMenuItems.Clear();
            foreach (var source in sources) {
                var existing = _sourcesMenuItems.FirstOrDefault(x => x.Tag as string == source.Id);
                if (existing != null) {
                    existing.IsChecked = entry.ReferencedFrom(source.Id);
                } else {
                    _sourcesMenuItems.AddSorted(new MenuItem {
                        Header = source.DisplayName,
                        IsCheckable = true,
                        IsChecked = entry.ReferencedFrom(source.Id),
                        Tag = source.Id,
                        Command = ToggleSourceCommand,
                        CommandParameter = source.Id
                    }, MenuComparer.Instance);
                }
            }
        }

        private void UpdateCheckedMenuItems() {
            if (!_listsButtonInitialized) return;
            var entry = Model.Entry;
            foreach (var menuItem in _sourcesMenuItems) {
                menuItem.IsChecked = entry.ReferencedFrom(menuItem.Tag as string);
            }
        }

        private DispatcherTimer _timer;

        public ViewModel Model => (ViewModel)DataContext;

        private DateTime _sessionEndedUpdate;

        private bool RequiresUpdate() {
            if (GameWrapper.IsInGame || AcSharedMemory.Instance.IsLive) return false;

            var entry = Model.Entry;
            if (entry.Status != ServerStatus.Ready) {
                return false;
            }

            var now = DateTime.Now;
            if (now - entry.PreviousUpdateTime > (entry.IsBooked ? TimeSpan.FromSeconds(1) : SettingsHolder.Online.RefreshPeriod.TimeSpan)) {
                return true;
            }

            if (now - _sessionEndedUpdate > TimeSpan.FromMinutes(0.5) && entry.SessionEnd < now &&
                    entry.CurrentSessionType.HasValue && entry.CurrentSessionType.Value != Game.SessionType.Race) {
                _sessionEndedUpdate = now;
                return true;
            }

            return false;
        }

        private void OnTick(object sender, EventArgs e) {
            if (Application.Current.MainWindow?.IsActive != true) return;

            Model.Entry.OnTick();
            if (RequiresUpdate()) {
                Model.Entry.Update(ServerEntry.UpdateMode.Normal, true).Forget();
            }
        }

        private async void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            await Task.Delay(1);
            if (e.Handled) return;

            e.Handled = true;
            ToolBar.IsActive = !ToolBar.IsActive;
        }

        public class ViewModel : NotifyPropertyChanged, IComparer {
            private ServerEntry _entry;

            [NotNull]
            public ServerEntry Entry {
                get { return _entry; }
                private set {
                    if (Equals(value, _entry)) return;

                    _entry = value;
                    OnPropertyChanged();

                    FancyBackgroundManager.Instance.ChangeBackground(value.Track?.PreviewImage);
                }
            }

            public void ChangeEntry([NotNull] ServerEntry entry) {
                Entry = entry;
            }

            public ViewModel([NotNull] ServerEntry entry) {
                Entry = entry;
                if (Entry.SelectedCarEntry == null) {
                    Entry.LoadSelectedCar();
                }
            }

            int IComparer.Compare(object x, object y) {
                return string.Compare((x as CarEntry)?.CarObject?.DisplayName, (y as CarEntry)?.CarObject?.DisplayName, StringComparison.CurrentCulture);
            }

            private DelegateCommand _manageListsCommand;

            public DelegateCommand ManageListsCommand => _manageListsCommand ?? (_manageListsCommand = new DelegateCommand(() => {
                new OnlineListsManager().ShowDialog();
            }));
        }

        private void SkinsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var entry = Model.Entry;
            if (entry.IsBooked && entry.BookingTimeLeft > TimeSpan.FromSeconds(3)) {
                entry.RebookSkin().Forget();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += OnTick;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;

            _timer?.Stop();
            _timer = null;

            Model.Entry.PropertyChanged -= Entry_PropertyChanged;
            DisposeHelper.Dispose(ref _holder);

            if (_listsButtonInitialized) {
                FileBasedOnlineSources.Instance.Update -= OnUserSourcesUpdated;
            }
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerEntry.Status):
                    UpdateCarsView();
                    break;
                case nameof(ServerEntry.SelectedCarEntry):
                    UpdateSelectedCar();
                    break;
                case nameof(ServerEntry.ReferencesString):
                    UpdateIcons();
                    UpdateCheckedMenuItems();
                    break;
            }
        }

        private IEnumerable<CarEntry> GetCars() {
            var cars = Model.Entry.Cars;
            if (cars == null) return new CarEntry[0];
            return cars.OrderBy(x => x.CarObject?.DisplayName).ToList();
        }

        private void UpdateCarsView() {
            CarsComboBox.ItemsSource = GetCars();
            if (ShowExtendedInformation) {
                CarsListBox.ItemsSource = CarsComboBox.ItemsSource;
            }

            UpdateSelectedCar();
        }

        private void UpdateSelectedCar() {
            CarsComboBox.SelectedItem = Model.Entry.SelectedCarEntry;
            if (ShowExtendedInformation) {
                CarsListBox.SelectedItem = CarsComboBox.SelectedItem;
            }
        }

        private void CarsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var carEntry = ((Selector)sender).SelectedItem as CarEntry;
            if (carEntry != null) {
                Model.Entry.SelectedCarEntry = carEntry;
            }
        }

        private void UpdateIcons() {
            var children = IconsPanel.Children;
            children.Clear();

            foreach (var originId in Model.Entry.GetReferencesIds()) {
                if (originId == KunosOnlineSource.Key || originId == LanOnlineSource.Key || originId == FileBasedOnlineSources.FavouritesKey ||
                        originId == FileBasedOnlineSources.RecentKey || originId == FileBasedOnlineSources.HiddenKey) {
                    continue;
                }

                var information = FileBasedOnlineSources.Instance.GetInformation(originId);
                if (information?.Label == null) {
                    continue;
                }

                var baseIcon = (Decorator)TryFindResource(@"BaseIcon");
                if (baseIcon == null) {
                    continue;
                }

                var text = (BbCodeBlock)baseIcon.Child;

                text.SetBinding(BbCodeBlock.BbCodeProperty, new Binding {
                    Path = new PropertyPath(nameof(information.Label)),
                    Source = information
                });

                text.SetBinding(TextBlock.ForegroundProperty, new Binding {
                    Path = new PropertyPath(nameof(information.Color)),
                    TargetNullValue = new SolidColorBrush(Colors.White),
                    Converter = ColorPicker.ColorToBrushConverter,
                    Source = information
                });

                baseIcon.Margin = new Thickness(0d, 0d, 4d, 0);
                children.Add(baseIcon);
            }
        }

        private void Driver_OnContextMenuButtonClick(object sender, ContextMenuButtonEventArgs e) {
            var driver = ((FrameworkElement)sender).DataContext as ServerEntry.CurrentDriver;
            if (driver == null) return;

            var menu = new ContextMenu();
            foreach (var tag in ServerEntry.DriverTag.GetTags().OrderBy(x => x.DisplayName)) {
                var icon = TryFindResource(@"PlayerTagIcon") as Shape;
                icon?.SetBinding(Shape.FillProperty, new Binding {
                    Source = tag,
                    Path = new PropertyPath(nameof(tag.Color)),
                    Converter = ColorPicker.ColorToBrushConverter
                });

                var header = new TextBlock();
                header.SetBinding(TextBlock.TextProperty, new Binding {
                    Source = tag,
                    Path = new PropertyPath(nameof(tag.DisplayName))
                });

                var item = new MenuItem {
                    Header = icon == null ? (FrameworkElement)header : new DockPanel { Children = { icon, header } },
                    IsChecked = driver.Tags.GetByIdOrDefault(tag.Id) != null,
                    StaysOpenOnClick = true,
                    IsCheckable = true
                };

                item.Click += (o, args) => {
                    (((MenuItem)o).IsChecked ? driver.AddTagCommand : driver.RemoveTagCommand).Execute(tag.Id);
                };

                menu.Items.Add(item);
            }

            menu.AddSeparator();

            menu.Items.Add(new MenuItem {
                Header = "New Tag…",
                Command = new DelegateCommand(() => {
                    new OnlineNewDriverTag(driver).ShowDialog();
                })
            });

            menu.Items.Add(new MenuItem {
                Header = "Edit Tags…",
                Command = new DelegateCommand(() => {
                    new OnlineDriverTags().ShowDialog();
                })
            });

            e.Menu = menu;
        }
    }
}