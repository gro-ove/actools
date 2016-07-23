using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class Online_SelectedServerPage : ILoadableContent, IParametrizedUriContent {
        private OnlineManagerType _type;
        private BaseOnlineManager _manager;
        private ServerEntry _entry;

        public void OnUri(Uri uri) {
            _type = uri.GetQueryParamEnum<OnlineManagerType>("Mode");
            _manager = BaseOnlineManager.ManagerByMode(_type);

            var id = uri.GetQueryParam("Id");
            if (id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }

            _entry = _manager.GetById(id);
            if (_entry == null) {
                throw new Exception(string.Format(AppStrings.Online_ServerWithIdIsMissing, id));
            }
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return _entry.Status == ServerStatus.Unloaded ? _entry.Update(ServerEntry.UpdateMode.Normal) :
                    Task.Delay(0, cancellationToken);
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
                new InputBinding(Model.Entry.RefreshCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(Model.Entry.JoinCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
        }

        private DispatcherTimer _timer;

        public ViewModel Model => (ViewModel)DataContext;

        private void Timer_Tick(object sender, EventArgs e) {
            Model.Entry.OnTick();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += Timer_Tick;
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
            public ServerEntry Entry { get; }

            public ViewModel(ServerEntry entry) {
                Entry = entry;
                FancyBackgroundManager.Instance.ChangeBackground(Entry.Track?.PreviewImage);
            }
        }
    }
}
