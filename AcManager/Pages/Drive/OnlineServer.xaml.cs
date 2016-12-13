using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using CarEntry = AcManager.Tools.Managers.Online.ServerEntry.CarEntry;

namespace AcManager.Pages.Drive {
    internal class WrappedCommand : ICommand {
        private readonly Func<ICommand> _provider;

        public WrappedCommand(Func<ICommand> provider) {
            _provider = provider;
        }

        public bool CanExecute(object parameter) {
            return true;
        }

        public void Execute(object parameter) {
            _provider()?.Execute(parameter);
        }

        public event EventHandler CanExecuteChanged {
            add { }
            remove { }
        }
    }

    public partial class OnlineServer : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            var entry = OnlineManager.Instance.GetById(id);
            if (entry == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }

            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Forget();
            }

            DataContext = new ViewModel(entry);
            InitializeComponent();
            UpdateCarsView();
            UpdateIcons();

            InputBindings.AddRange(new[] {
                new InputBinding(new WrappedCommand(() => Model.Entry.RefreshCommand), new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(new WrappedCommand(() => Model.Entry.JoinCommand), new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(new WrappedCommand(() => Model.Entry.CancelBookingCommand), new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(new WrappedCommand(() => Model.Entry.JoinCommand), new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)) {
                    CommandParameter = ServerEntry.ForceJoin
                }
            });
        }

        public void Change(ServerEntry entry) {
            if (entry.Status == ServerStatus.Unloaded) {
                entry.Update(ServerEntry.UpdateMode.Normal).Forget();
            }
            
            Model.Entry.PropertyChanged -= Entry_PropertyChanged;
            Model.ChangeEntry(entry);
            UpdateCarsView();
            UpdateIcons();
            Model.Entry.PropertyChanged += Entry_PropertyChanged;
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
                Model.Entry.Update(ServerEntry.UpdateMode.Full, true).Forget();
            }
        }

        private async void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            await Task.Delay(1);
            if (e.Handled) return;

            e.Handled = true;
            ToolBar.IsActive = !ToolBar.IsActive;
        }

        private void ToolBar_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;
            ToolBar.IsActive = false;
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
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerEntry.Cars):
                    UpdateCarsView();
                    break;
                case nameof(ServerEntry.OriginsString):
                    UpdateIcons();
                    break;
            }
        }

        private IEnumerable<CarEntry> GetCars() {
            return (IEnumerable<CarEntry>)Model.Entry.Cars?.OrderBy(x => x.CarObject?.DisplayName) ?? new CarEntry[0];
        }

        private void UpdateCarsView() {
            CarsComboBox.ItemsSource = GetCars();
            CarsComboBox.SelectedItem = Model.Entry.SelectedCarEntry;
        }

        private void CarsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var carEntry = CarsComboBox.SelectedItem as CarEntry;
            if (carEntry != null) {
                Model.Entry.SelectedCarEntry = carEntry;
            }
        }

        private void UpdateIcons() {
            var children = IconsPanel.Children;
            children.Clear();

            foreach (var originId in Model.Entry.GetOriginsIds()) {
                if (originId == KunosOnlineSource.Key || originId == LanOnlineSource.Key || originId == FileBasedOnlineSources.FavoritesKey ||
                        originId == FileBasedOnlineSources.RecentKey) {
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
    }
}
