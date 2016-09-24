using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class Online_SelectedServerPage : ILoadableContent, IParametrizedUriContent {
        private OnlineManagerType _type;
        private BaseOnlineManager _managerOld;
        private ServerEntry _entryOld;

        public void OnUri(Uri uri) {
            _type = uri.GetQueryParamEnum<OnlineManagerType>("Mode");
            _managerOld = BaseOnlineManager.ManagerByMode(_type);

            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            _entryOld = _managerOld.GetById(id);
            if (_entryOld == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return _entryOld.Status == ServerStatus.Unloaded ? _entryOld.Update(ServerEntry.UpdateMode.Normal) :
                    Task.Delay(0, cancellationToken);
        }

        public void Load() {
            if (_entryOld.Status == ServerStatus.Unloaded) {
                _entryOld.Update(ServerEntry.UpdateMode.Normal).Forget();
            }
        }

        public void Initialize() {
            DataContext = new ViewModel(_entryOld);
            InitializeComponent();

            if (Model.EntryOld == null) return;
            InputBindings.AddRange(new[] {
                new InputBinding(Model.EntryOld.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(Model.EntryOld.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.EntryOld.CancelBookingCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.EntryOld.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)) {
                    CommandParameter = ServerEntry.ForceJoin
                }
            });
        }

        private DispatcherTimer _timer;

        public ViewModel Model => (ViewModel)DataContext;

        private DateTime _sessionEndedUpdate;

        private bool RequiresUpdate() {
            var entry = Model.EntryOld;
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

            Model.EntryOld.OnTick();
            if (RequiresUpdate()) {
                Model.EntryOld.Update(ServerEntry.UpdateMode.Full, true).Forget();
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

        public class ViewModel : NotifyPropertyChanged {
            public ServerEntry EntryOld { get; }

            public ViewModel(ServerEntry entryOld) {
                EntryOld = entryOld;
                FancyBackgroundManager.Instance.ChangeBackground(EntryOld.Track?.PreviewImage);
            }
        }

        private void SkinsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var entry = Model.EntryOld;
            if (entry.IsBooked && entry.BookingTimeLeft > TimeSpan.FromSeconds(3)) {
                entry.RebookSkin().Forget();
            }
        }
    }
}
