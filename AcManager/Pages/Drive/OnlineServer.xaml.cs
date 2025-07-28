using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.DiscordRpc;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using CarEntry = AcManager.Tools.Managers.Online.ServerEntry.CarEntry;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer : IParametrizedUriContent, IImmediateContent {
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(100, "Preparing to race", "Online");

        public static TimeSpan OptionAutoConnectPeriod = TimeSpan.FromSeconds(5d);

        private Holder<ServerEntry> _holder;
        private Sublist<MenuItem> _sourcesMenuItems;

        public OnlineServer() { }

        public OnlineServer([NotNull] ServerEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            _holder = new Holder<ServerEntry>(entry, t => { });
            Initialize();
            this.OnActualUnload(_discordPresence);
        }

        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            _holder = OnlineManager.Instance.HoldById(id);
            if (_holder == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }

            Initialize();
        }

        private const string KeyCopyPasswordToInviteLink = "Online.CopyPasswordToInviteLink";

        public static bool IncludePasswordToInviteLink => ValuesStorage.Get(KeyCopyPasswordToInviteLink, true);

        private void Initialize() {
            var entry = _holder.Value;
            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Ignore();
            }

            DataContext = new ViewModel(entry, _discordPresence);
            InitializeComponent();
            AssistsPopup.DataContext = AssistsDescription.DataContext = Model.Assists;

            var list = ListsButton.MenuItems;
            _sourcesMenuItems = new Sublist<MenuItem>(list, list.Count - 1, 0);

            UpdateCarsView();
            UpdateIcons();
            UpdateBindings();
            Model.Entry.PropertyChanged += OnEntryPropertyChanged;
            ResizingStuff();

            _discordPresence.Details = $"Online | {entry.DisplayName}";
        }

        private bool? _showExtendedInformation;

        public bool ShowExtendedInformation {
            get => _showExtendedInformation ?? false;
            set {
                if (Equals(value, _showExtendedInformation)) return;
                _showExtendedInformation = value;

                if (value) {
                    ExtendedInformation.Visibility = Visibility.Visible;
                    ScrollViewer.Margin = new Thickness(0d, 0d, -4d, 0d);
                    ScrollViewer.Padding = new Thickness(0d, 0d, 4d, 8d);
                    CarsListBox.ItemsSource = CarsComboBox.ItemsSource;
                    CarsListBox.SelectedItem = CarsComboBox.SelectedItem;
                } else {
                    ExtendedInformation.Visibility = Visibility.Collapsed;
                    ScrollViewer.Margin = new Thickness(0d, 0d, -8d, 0d);
                    ScrollViewer.Padding = new Thickness(0d, 0d, 8d, 8d);
                }
            }
        }

        private bool? _scrollableContent;

        public bool ScrollableContent {
            get => _scrollableContent ?? false;
            set {
                if (Equals(value, _scrollableContent)) return;
                _scrollableContent = value;
                ScrollViewer.VerticalScrollBarVisibility = value ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            }
        }

        private void ResizingStuff() {
            ShowExtendedInformation = ActualWidth > 600;

            // var contentHeight = ErrorsPanel.ActualHeight + PasswordPanel.ActualHeight + DataPanel.ActualHeight;
            // var availableHeight = ScrollViewer.ActualHeight;
            // ScrollableContent = availableHeight - contentHeight < 120;
            ScrollableContent = true;
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
            FileBasedOnlineSources.Instance.Update -= OnUserSourcesUpdated;
            FileBasedOnlineSources.Instance.Update += OnUserSourcesUpdated;
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var existing = _holder;
            _holder = OnlineManager.Instance.HoldById(id);
            DisposeHelper.Dispose(ref existing);
            if (_holder == null) return false;

            var entry = _holder.Value;
            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Ignore();
            }

            Model.AutoJoinActive = false;
            Model.Entry.PropertyChanged -= OnEntryPropertyChanged;
            Model.ChangeEntry(entry);
            UpdateCarsView();
            UpdateIcons();
            UpdateBindings();
            UpdateCheckedMenuItems();
            Model.Entry.PropertyChanged += OnEntryPropertyChanged;
            Model.Entry.RaiseSelectedCarChanged();
            CommandManager.InvalidateRequerySuggested();
            return true;
        }

        private void UpdateBindings() {
            InputBindings.Clear();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.Entry.ToggleFavouriteCommand, new KeyGesture(Key.B, ModifierKeys.Control)),
                new InputBinding(Model.Entry.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.Entry.ToggleFavouriteCommand, new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(Model.InviteCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
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
                return string.Compare(x?.Header?.ToString(), y?.Header?.ToString(), StringComparison.CurrentCulture);
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
                        CommandParameter = source.Id,
                        StaysOpenOnClick = true
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
            var updateTime = entry.IsBooked ? TimeSpan.FromSeconds(1) : SettingsHolder.Online.RefreshPeriod.TimeSpan;
            if (Model.AutoJoinActive) {
                updateTime = updateTime.Min(OptionAutoConnectPeriod);
            }

            if (now - entry.PreviousUpdateTime >= updateTime - TimeSpan.FromSeconds(0.1)) {
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
            if (Application.Current?.MainWindow?.IsActive != true && !Model.AutoJoinActive) return;

            Model.Entry.OnTick();
            if (RequiresUpdate()) {
                Model.Entry.Update(ServerEntry.UpdateMode.Normal, true).Ignore();
            }
        }

        private async void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            await Task.Delay(1);
            if (e.Handled) return;

            e.Handled = true;
            ToolBar.IsActive = !ToolBar.IsActive;
        }

        public partial class ViewModel : NotifyPropertyChanged, IComparer {
            public QuickDriveButtonModel QuickDriveButton { get; }

            [CanBeNull]
            private readonly DiscordRichPresence _discordPresence;

            public AssistsViewModel Assists { get; } = new AssistsViewModel("onlineassists");

            private ServerEntry _entry;

            [NotNull]
            public ServerEntry Entry {
                get => _entry;
                private set {
                    if (Equals(value, _entry)) return;

                    _entry = value;
                    OnPropertyChanged();

                    FancyBackgroundManager.Instance.ChangeBackground(value.Track?.PreviewImage);
                    _discordPresence?.Car(value.SelectedCarEntry?.CarObject).Track(value.Track);

                    value.SubscribeWeak((sender, args) => {
                        if (args.PropertyName == nameof(value.SelectedCarEntry)) {
                            _discordPresence?.Car(value.SelectedCarEntry?.CarObject);
                        }
                    });

                    if (value.ActualName != null) {
                        _tsServer = TsRegex.Match(value.ActualName).Groups.OfType<Group>().ElementAtOrDefault(1)?.Value;
                        _joinTsCommand?.RaiseCanExecuteChanged();
                    }
                }
            }

            public void ChangeEntry([NotNull] ServerEntry entry) {
                Entry = entry;
                Assists.ServerAssists = Entry.AssistsInformation;
                Entry.Assists = Assists.ToGameProperties();
            }

            private static readonly Regex TsRegex = new Regex(@"\s+(?:teamspeak|ts):?\s*((?:(?:[12]?\d{1,2})\.){3}(?:[12]?\d{1,2})(?::\d+)?)",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

            internal ViewModel([NotNull] ServerEntry entry, [CanBeNull] DiscordRichPresence discordPresence) {
                _discordPresence = discordPresence;

                Entry = entry;
                Assists.ServerAssists = Entry.AssistsInformation;

                if (Entry.SelectedCarEntry == null) {
                    Entry.LoadSelectedCar();
                }

                Entry.Assists = Assists.ToGameProperties();
                Assists.Changed += OnAssistsChanged;

                QuickDriveButton = new QuickDriveButtonModel((setupRace, preset) => {
                    Uri mode = null;
                    if (Entry.Sessions?.Any(x => x.Type == Game.SessionType.Practice) == true) {
                        mode = Entry.Sessions?.Any(x => x.Type == Game.SessionType.Qualification) == true
                                && Entry.Sessions?.Any(x => x.Type == Game.SessionType.Race) == true ? QuickDrive.ModeWeekend : QuickDrive.ModePractice;
                    } else if (Entry.Sessions?.Any(x => x.Type == Game.SessionType.Race) == true) {
                        mode = QuickDrive.ModeRace;
                    } else if (Entry.Sessions?.Any(x => x.Type == Game.SessionType.Qualification) == true) {
                        mode = QuickDrive.ModeHotlap;
                    }
                    return QuickDrive.Activate(setupRace, Entry.SelectedCarEntry?.CarObject, Entry.SelectedCarEntry?.AvailableSkinId,
                            track: Entry.Track, presetFilename: preset, weatherId: Entry.WeatherId, mode: mode, time: Entry.TimeSeconds,
                            serializedRaceGrid: RaceGridViewModel.GeneratePresetData((Entry.Cars?.SelectMany(x => {
                                if (x.CarObject == null || x.Available == 0) return null;
                                return Enumerable.Range(0, x.Available).Select(y => new RaceGridEntry(x.CarObject) { CarSkin = x.AvailableSkin });
                            }) ?? new RaceGridEntry[0]).Concat(Entry.CurrentDrivers?.Select(x => {
                                if (x.IsBookedForPlayer) return null;
                                return new RaceGridEntry(x.Car) { CarSkin = x.CarSkin, Name = x.Name };
                            }) ?? new RaceGridEntry[0])));
                });
            }

            private void OnAssistsChanged(object sender, EventArgs eventArgs) {
                Entry.Assists = Assists.ToGameProperties();
            }

            int IComparer.Compare(object x, object y) {
                return string.Compare((x as CarEntry)?.CarObject?.DisplayName, (y as CarEntry)?.CarObject?.DisplayName, StringComparison.CurrentCulture);
            }

            private DelegateCommand _manageListsCommand;

            public DelegateCommand ManageListsCommand
                => _manageListsCommand ?? (_manageListsCommand = new DelegateCommand(() => { new OnlineListsManager().ShowDialog(); }));

            private bool _copyPasswordToInviteLink = IncludePasswordToInviteLink;

            public bool CopyPasswordToInviteLink {
                get => _copyPasswordToInviteLink;
                set {
                    if (Equals(value, _copyPasswordToInviteLink)) return;
                    _copyPasswordToInviteLink = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyCopyPasswordToInviteLink, value);
                }
            }

            private DelegateCommand _inviteCommand;

            public DelegateCommand InviteCommand => _inviteCommand ?? (_inviteCommand = new DelegateCommand(() => {
                var link = $@"{InternalUtils.MainApiDomain}/s/q:race/online/join?ip={Entry.Ip}&httpPort={Entry.PortHttp}";
                if (CopyPasswordToInviteLink && !string.IsNullOrWhiteSpace(Entry.Password) && Entry.PasswordRequired) {
                    link += $@"&password={EncryptSharedPassword(Entry.Id, Entry.Password)}";
                }

                SharingUiHelper.ShowShared("Inviting link", link, false);
            }));

            private string _tsServer;
            private DelegateCommand _joinTsCommand;

            public DelegateCommand JoinTsCommand => _joinTsCommand ?? (_joinTsCommand = new DelegateCommand(() => {
                try {
                    var index = _tsServer.IndexOf(':');
                    var query = index == -1 ?
                            $"ts3server://{_tsServer}?port=9987" :
                            $"ts3server://{_tsServer.Substring(0, index)}?port={_tsServer.Substring(index + 1)}";
                    if (Process.Start(query) == null) {
                        throw new InformativeException("Can’t start TeamSpeak", "Make sure it’s installed and handles its links (“ts3server://…”) properly.");
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t start TeamSpeak", e);
                }
            }, () => _tsServer != null));
        }

        /// <summary>
        /// Of course, it’s the stupidest way to crypt anything, but all “encrypted” data still
        /// could be very easily decrypted just by using CM (then, password will end up in race.ini
        /// in a plain view). And this way at least result string is small.
        /// </summary>
        private static readonly string EncryptKey = InternalUtils.GetOnlineInviteEncryptionKey();

        private static void Xor(byte[] data, byte[] key) {
            int dataLength = data.Length, keyLength = key.Length;
            for (int i = 0, k = 0; i < dataLength; i++, k++) {
                if (k == keyLength) k = 0;
                data[i] ^= key[k];
            }
        }

        public static string EncryptSharedPassword(string id, string password) {
            var data = Encoding.UTF8.GetBytes(password);
            Xor(data, Encoding.UTF8.GetBytes(id + EncryptKey));
            return HttpUtility.UrlEncode(Convert.ToBase64String(data));
        }

        public static string EncryptSharedPassword(string ip, int httpPort, string password) {
            return HttpUtility.UrlEncode(EncryptSharedPassword($@"{ip}:{httpPort.ToInvariantString()}", password));
        }

        public static string DecryptSharedPassword(string id, string encryptedPassword) {
            var data = Convert.FromBase64String(encryptedPassword);
            Xor(data, Encoding.UTF8.GetBytes(id + EncryptKey));
            return Encoding.UTF8.GetString(data);
        }

        public static string DecryptSharedPassword(string ip, int httpPort, string encryptedPassword) {
            return DecryptSharedPassword($@"{ip}:{httpPort.ToInvariantString()}", encryptedPassword);
        }

        private void SkinsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var entry = Model.Entry;
            if (entry.IsBooked && entry.BookingTimeLeft > TimeSpan.FromSeconds(3)) {
                entry.RebookSkin().Ignore();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(0.5),
                IsEnabled = true
            };
            _timer.Tick += OnTick;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;

            _timer?.Stop();
            _timer = null;

            Model.Entry.PropertyChanged -= OnEntryPropertyChanged;
            Model.QuickDriveButton.Dispose();
            DisposeHelper.Dispose(ref _holder);

            if (_listsButtonInitialized) {
                FileBasedOnlineSources.Instance.Update -= OnUserSourcesUpdated;
            }
        }

        private void OnEntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            Model.OnPropertyChanged(e);

            switch (e.PropertyName) {
                case nameof(ServerEntry.Status):
                    UpdateCarsView();
                    ResizingStuff();
                    CommandManager.InvalidateRequerySuggested();
                    break;
                case nameof(ServerEntry.AssistsInformation):
                    Model.Assists.ServerAssists = Model.Entry.AssistsInformation;
                    break;
                case nameof(ServerEntry.ErrorsString):
                    ResizingStuff();
                    UpdateCarsView();
                    break;
                case nameof(ServerEntry.PasswordRequired):
                case nameof(ServerEntry.HasDetails):
                case nameof(ServerEntry.Description):
                    ResizingStuff();
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
            AcContext.Instance.CurrentCar = Model.Entry.SelectedCarEntry?.CarObject;

            CarsComboBox.SelectedItem = Model.Entry.SelectedCarEntry;
            if (ShowExtendedInformation) {
                CarsListBox.SelectedItem = CarsComboBox.SelectedItem;
            }
        }

        private void OnCarsComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Model.Entry.FixedCar) {
                ((Selector)sender).SelectedItem = Model.Entry.SelectedCarEntry;
            } else if (((Selector)sender).SelectedItem is CarEntry carEntry) {
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

                text.SetBinding(BbCodeBlock.TextProperty, new Binding {
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

        private void OnDriverContextMenu(object sender, ContextMenuButtonEventArgs e) {
            if (!(((FrameworkElement)sender).DataContext is ServerEntry.CurrentDriver driver)) return;

            var menu = new ContextMenu();
            foreach (var tag in ServerEntry.DriverTag.GetTags().OrderBy(x => x.DisplayName)) {
                var icon = TryFindResource(@"PlayerTagIcon") as Shape;
                icon?.SetBinding(Shape.FillProperty, new Binding {
                    Source = tag,
                    Path = new PropertyPath(nameof(tag.Color)),
                    Converter = ColorPicker.ColorToBrushConverter
                });

                var header = new BbCodeBlock();
                header.SetBinding(BbCodeBlock.TextProperty, new Binding {
                    Source = tag,
                    Path = new PropertyPath(nameof(tag.DisplayName))
                });

                var item = new MenuItem {
                    Header = icon == null ? (FrameworkElement)header : new DockPanel { Children = { icon, header } },
                    IsChecked = driver.Tags.GetByIdOrDefault(tag.Id) != null,
                    StaysOpenOnClick = true,
                    IsCheckable = true
                };

                item.Click += (o, args) => { (((MenuItem)o).IsChecked ? driver.AddTagCommand : driver.RemoveTagCommand).Execute(tag.Id); };

                menu.Items.Add(item);
            }

            menu.AddSeparator();

            menu.Items.Add(new MenuItem {
                Header = "New Tag…",
                Command = new DelegateCommand(() => { new OnlineNewDriverTag(driver).ShowDialog(); })
            });

            menu.Items.Add(new MenuItem {
                Header = "Edit Tags…",
                Command = new DelegateCommand(() => { new OnlineDriverTags().ShowDialog(); })
            });

            e.Menu = menu;
        }

        private void OnShareStatsItemClick(object sender, RoutedEventArgs e) {
            var server = Model.Entry;
            var stats = server.Stats;
            if (stats.Distance <= 100d || server.LastConnected == null) {
                ModernDialog.ShowMessage("You need to drive at least 100 meters for that");
                return;
            }

            if (double.IsNaN(stats.Distance)) {
                ModernDialog.ShowMessage("Stats are damaged");
                return;
            }

            ((MenuItem)sender).IsEnabled = false;

            var driverName = SettingsHolder.Drive.DifferentPlayerNameOnline
                    ? SettingsHolder.Drive.PlayerNameOnline.Or(SettingsHolder.Drive.PlayerName)
                    : SettingsHolder.Drive.PlayerName;
            var steamId = SteamIdHelper.Instance.Value;
            if (steamId != null && steamId.Length > 4) {
                driverName += $" (…{steamId.Substring(steamId.Length - 4)})";
            }

            SharingUiHelper.ShareAsync(SharedEntryType.Results,
                    "Online stats", null,
                    $@"Server stats for {driverName} at {DateTime.Now:dd/MM/yyyy hh:mm tt}:
• Server: {server.ActualName?.Trim()} ({server.Id});
• Connected {PluralizingConverter.PluralizeExt(stats.SessionsCount, "{0} time")}, last time at {server.LastConnected:dd/MM/yyyy hh:mm tt};
• Counting from {(server.CountingStatsFrom == null ? @"unknown time" : $@"{server.CountingStatsFrom:dd/MM/yyyy hh:mm tt}")};
• Driven in total {stats.Distance / 1e3:F1} km ({stats.Time.ToReadableTime()});
• Had {PluralizingConverter.PluralizeExt(stats.TotalCrashes, "{0} crash")} crashes, gone offroad {PluralizingConverter.PluralizeExt(stats.GoneOffroadTimes,
        "{0} time")};
• Avg. speed: {stats.AverageSpeed:F1} km/h, top speed: {stats.MaxSpeed:F1} km/h;
• Burnt {stats.FuelBurnt:F1} l of fuel, worn out {PluralizingConverter.PluralizeExt(stats.TotalTyreWearRounded.RoundToInt(), "{0} tyre")}.");
        }

        private void OnDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            Model.QuickDriveButton.Initialize();
        }

        private async void OnErrorsBlockLoaded(object sender, RoutedEventArgs e) {
            if (Model.Entry.IsAbleToInstallMissingContentState_Cup == ServerEntry.IsAbleToInstallMissingContent.NoMissingContent
                    || FancyHints.CupV2IndexedContent.Shown) {
                return;
            }

            await Task.Yield();
            var self = (SelectableBbCodeBlock)sender;
            var downloadIcon = BbCodeBlock.IconsDictionary[@"DownloadIconData"] as Geometry;
            foreach (var child in self.FindVisualChildren<Path>()) {
                if (child.Data == downloadIcon) {
                    var pos = child.TranslatePoint(new Point(0, 0), self);
                    FancyHintsService.SetOffsetX(self, pos.X + child.ActualWidth / 2d);
                    FancyHintsService.SetOffsetY(self, pos.Y + child.ActualHeight / 2d);
                    FancyHints.CupV2IndexedContent.Trigger(TimeSpan.FromSeconds(1d));
                    break;
                }
            }
        }
    }
}