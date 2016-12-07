using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

using CarEntry = AcManager.Tools.Managers.Online.ServerEntry.CarEntry;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer : ILoadableContent, IParametrizedUriContent {
        private ServerEntry _entry;

        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            _entry = OnlineManager.Instance.GetById(id);
            if (_entry == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            Load();
            return Task.Delay(0, cancellationToken);
            //return _entryOld.Status == ServerStatus.Unloaded ? _entryOld.Update(ServerEntry.UpdateMode.Normal) :
            //        Task.Delay(0, cancellationToken);
        }

        public void Load() {
            if (_entry.Status == ServerStatus.Unloaded) {
                _entry.Update(ServerEntry.UpdateMode.Normal).Forget();
            }
        }

        public void Initialize() {
            DataContext = new ViewModel(_entry);
            InitializeComponent();

            if (Model.Entry == null) return;
            InputBindings.AddRange(new[] {
                new InputBinding(Model.Entry.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.Entry.CancelBookingCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)) {
                    CommandParameter = ServerEntry.ForceJoin
                }
            });
        }

        private DispatcherTimer _timer;

        public ViewModel Model => (ViewModel)DataContext;

        private DateTime _sessionEndedUpdate;

        private bool RequiresUpdate() {
            var entry = Model.Entry;
            if (entry.Status != ServerStatus.Ready) {
                return false;
            }

            var now = DateTime.Now;

            if (now - entry.PreviousUpdateTime > TimeSpan.FromSeconds(5)) {
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

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += OnTick;

            Model.OnLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;

            _timer?.Stop();
            _timer = null;

            Model.OnUnloaded();
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
            public ServerEntry Entry { get; }

            public BetterObservableCollection<CarEntry> Cars { get; }

            public ViewModel(ServerEntry entry) {
                Entry = entry;
                Cars = new BetterObservableCollection<CarEntry>(GetCars());
                FancyBackgroundManager.Instance.ChangeBackground(Entry.Track?.PreviewImage);
            }

            private IEnumerable<CarEntry> GetCars() {
                return (IEnumerable<CarEntry>)Entry.Cars?.OrderBy(x => x.CarObject?.DisplayName) ?? new CarEntry[0];
            }

            private void UpdateCarsView() {
                Cars.ReplaceIfDifferBy(GetCars());
            }

            int IComparer.Compare(object x, object y) {
                return string.Compare((x as CarEntry)?.CarObject?.DisplayName, (y as CarEntry)?.CarObject?.DisplayName, StringComparison.CurrentCulture);
            }

            public void OnLoaded() {
                Entry.PropertyChanged += Entry_PropertyChanged;
            }

            public void OnUnloaded() {
                Entry.PropertyChanged -= Entry_PropertyChanged;
            }

            private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(ServerEntry.Cars):
                        UpdateCarsView();
                        break;
                }
            }
        }

        private void SkinsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var entry = Model.Entry;
            if (entry.IsBooked && entry.BookingTimeLeft > TimeSpan.FromSeconds(3)) {
                entry.RebookSkin().Forget();
            }
        }
    }
}
