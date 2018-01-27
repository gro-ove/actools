using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.DiscordRpc;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Drive {
    public partial class KunosCareer_SelectedPage : ILoadableContent, IParametrizedUriContent {
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(10, "Preparing to race", "Career").Default();

        public void OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await KunosCareerManager.Instance.EnsureLoadedAsync();

            var acObject = await KunosCareerManager.Instance.GetByIdAsync(_id);
            if (acObject == null || acObject.HasErrors || !acObject.IsAvailable) {
                KunosCareer.NavigateToCareerPage(null);
                return;
            }

            await acObject.EnsureEventsLoadedAsync();
            _discordPresence?.Default(acObject.DisplayName);
            DataContext = new ViewModel(acObject);
        }

        public void Load() {
            KunosCareerManager.Instance.EnsureLoaded();

            var acObject = KunosCareerManager.Instance.GetById(_id);
            if (acObject == null || acObject.HasErrors || !acObject.IsAvailable) {
                KunosCareer.NavigateToCareerPage(null);
                return;
            }

            acObject.EnsureEventsLoaded();
            _discordPresence?.Default(acObject.DisplayName);
            DataContext = new ViewModel(acObject);
        }

        public void Initialize() {
            this.OnActualUnload(_discordPresence);

            if (!(DataContext is ViewModel)) return;
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(() => Model.AcObject.SelectedEvent?.GoCommand.Execute()), new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => KunosCareer.NavigateToCareerPage(null)), new KeyGesture(Key.W, ModifierKeys.Control)),
            });

            var acObject = Model.AcObject;
            if (acObject.LastSelectedTimestamp != 0) return;

            if (File.Exists(acObject.StartVideo)) {
                //if (VideoViewer.IsSupported()) {
                    new VideoViewer(acObject.StartVideo, acObject.Name).ShowDialog();
                //}
            }

            new KunosCareerIntro(acObject).ShowDialog();
            acObject.LastSelectedTimestamp = DateTime.Now.ToMillisecondsTimestamp();

            _discordPresence.LargeImage = new DiscordImage("", acObject.DisplayName);
        }

        private string _id;
        private ScrollViewer _scroll;
        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _scroll = ListBox.FindVisualChild<ScrollViewer>();
            if (_scroll != null) {
                _scroll.LayoutUpdated += ScrollLayoutUpdated;
            }

            if (_loaded) return;
            _loaded = true;

            Model.AcObject.AcObjectOutdated += AcObject_AcObjectOutdated;
        }

        private bool _positionLoaded;

        private void ScrollLayoutUpdated(object sender, EventArgs e) {
            if (_positionLoaded) return;
            var value = ValuesStorage.Get<double>(KeyScrollValue);
            _scroll?.ScrollToHorizontalOffset(value);
            _positionLoaded = true;
        }

        private void AcObject_AcObjectOutdated(object sender, EventArgs e) {
            var acObject = KunosCareerManager.Instance.GetById(Model.AcObject.Id);
            if (acObject == null || acObject.HasErrors || !acObject.IsAvailable) {
                KunosCareer.NavigateToCareerPage(null);
                return;
            }

            Model.AcObject.AcObjectOutdated -= AcObject_AcObjectOutdated;
            Model.AcObject = acObject;
            acObject.AcObjectOutdated += AcObject_AcObjectOutdated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            Model.AcObject.AcObjectOutdated -= AcObject_AcObjectOutdated;
        }

        private string KeyScrollValue => @"KunosCareer_SelectedPage.ListBox.Scroll__" + _id;

        private void ListBox_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scroll == null || !_positionLoaded) return;
            ValuesStorage.Set(KeyScrollValue, _scroll.HorizontalOffset);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private KunosCareerObject _acObject;

            public KunosCareerObject AcObject {
                get => _acObject;
                set {
                    if (Equals(value, _acObject)) return;
                    _acObject = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(KunosCareerObject careerObject) {
                _acObject = careerObject;
            }

            public AssistsViewModel AssistsViewModel => AssistsViewModel.Instance;

            public SettingsHolder.DriveSettings DriveSettings => SettingsHolder.Drive;
        }

        private void ListBox_OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            Model.AcObject.SelectedEvent?.GoCommand.Execute();
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(Model.AssistsViewModel).ShowDialog();
        }

        private void NextButton_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            var career = Model.AcObject.NextCareerObject;
            if (career == null) return;
            KunosCareer.NavigateToCareerPage(career);
        }

        private void TableSection_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void ResetButton_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (ModernDialog.ShowMessage(AppStrings.KunosCareer_ResetProgress_Message, AppStrings.KunosCareer_ResetProgress_Title,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Model.AcObject.ChampionshipResetCommand.Execute();
        }

        private void OnCarPreviewClick(object sender, MouseButtonEventArgs e) {
            var ev = Model.AcObject.SelectedEvent;
            if (ev == null) return;

            var control = new CarBlock {
                Car = ev.CarObject,
                SelectedSkin = ev.CarSkin,
                SelectSkin = SettingsHolder.Drive.KunosCareerUserSkin,
                OpenShowroom = true
            };

            var dialog = new ModernDialog {
                Content = control,
                Width = 640,
                Height = 720,
                MaxWidth = 640,
                MaxHeight = 720,
                SizeToContent = SizeToContent.Manual,
                Title = ev.CarObject.DisplayName
            };

            dialog.Buttons = new[] { dialog.OkButton, dialog.CancelButton };
            dialog.ShowDialog();

            if (dialog.IsResultOk && SettingsHolder.Drive.KunosCareerUserSkin) {
                ev.CarSkin = control.SelectedSkin;
            }
        }

        private async void OnChangeSkinMenuItemClick(object sender, MouseButtonEventArgs e) {
            var ev = Model.AcObject.SelectedEvent;
            if (ev == null) return;

            await ev.CarObject.SkinsManager.EnsureLoadedAsync();

            var skins = ev.CarObject.EnabledOnlySkins.ToList();
            var viewer = new ImageViewer(
                skins.Select(x => x.PreviewImage),
                skins.IndexOf(ev.CarSkin),
                CommonAcConsts.PreviewWidth,
                details: CarBlock.GetSkinImageViewerDetailsCallback(ev.CarObject));

            if (SettingsHolder.Drive.KunosCareerUserSkin) {
                var selected = viewer.ShowDialogInSelectMode();
                ev.CarSkin = skins.ElementAtOrDefault(selected ?? -1) ?? ev.CarSkin;
            } else {
                viewer.ShowDialog();
            }
        }

        private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!(ListBox.SelectedItem is KunosCareerEventObject ev)) return;
            FancyBackgroundManager.Instance.ChangeBackground(ev.TrackObject?.PreviewImage ?? ev.PreviewImage);
        }
    }
}
