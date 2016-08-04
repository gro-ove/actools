using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Drive {
    public partial class KunosCareer_SelectedPage : ILoadableContent, IParametrizedUriContent {
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
            DataContext = new ViewModel(acObject);
        }

        public void Initialize() {
            if (!(DataContext is ViewModel)) return;
            InitializeComponent();

            var acObject = Model.AcObject;
            if (acObject.LastSelectedTimestamp != 0) return;

            if (File.Exists(acObject.StartVideo)) {
                if (VideoViewer.IsSupported()) {
                    new VideoViewer(acObject.StartVideo, acObject.Name).ShowDialog();
                }

                new KunosCareerIntro(acObject).ShowDialog();
            }

            acObject.LastSelectedTimestamp = DateTime.Now.ToMillisecondsTimestamp();
        }

        private string _id;
        private ScrollViewer _scrollViewer;
        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _scrollViewer = ListBox.FindVisualChild<ScrollViewer>();
            _scrollViewer?.ScrollToHorizontalOffset(ValuesStorage.GetDoubleNullable(KeyScrollValue) ?? 0d);

            if (_loaded) return;
            _loaded = true;

            Model.AcObject.AcObjectOutdated += AcObject_AcObjectOutdated;
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
            if (_scrollViewer == null) return;
            ValuesStorage.Set(KeyScrollValue, _scrollViewer.HorizontalOffset);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private KunosCareerObject _acObject;

            public KunosCareerObject AcObject {
                get { return _acObject; }
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
            Model.AcObject.SelectedEvent?.GoCommand.Execute(null);
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
            Model.AcObject.ChampionshipResetCommand.Execute(null);
        }

        private void CarPreview_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
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

        private async void ChangeSkinMenuItem_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            var ev = Model.AcObject.SelectedEvent;
            if (ev == null) return;

            await ev.CarObject.SkinsManager.EnsureLoadedAsync();

            var viewer = new ImageViewer(
                ev.CarObject.Skins.Select(x => x.PreviewImage),
                ev.CarObject.Skins.IndexOf(ev.CarSkin)
            );

            if (SettingsHolder.Drive.KunosCareerUserSkin) {
                var selected = viewer.ShowDialogInSelectMode();
                ev.CarSkin = ev.CarObject.Skins.ElementAtOrDefault(selected ?? -1) ?? ev.CarSkin;
            } else {
                viewer.ShowDialog();
            }
        }

        private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var ev = ListBox.SelectedItem as KunosCareerEventObject;
            if (ev == null) return;
            FancyBackgroundManager.Instance.ChangeBackground(ev.TrackObject?.PreviewImage ?? ev.PreviewImage);
        }
    }
}
