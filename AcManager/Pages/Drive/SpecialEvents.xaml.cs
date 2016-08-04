using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Drive {
    public partial class SpecialEvents : ILoadableContent {
        public Task LoadAsync(CancellationToken cancellationToken) {
            return SpecialEventsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            SpecialEventsManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, IComparer {
            public AcWrapperCollectionView List { get; }

            private SpecialEventObject _selected;

            public SpecialEventObject Selected {
                get { return _selected; }
                set {
                    if (Equals(value, _selected)) return;
                    _selected = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel() {
                List = new AcWrapperCollectionView(SpecialEventsManager.Instance.WrappersAsIList);
                List.CurrentChanged += OnCurrentChanged;
                List.MoveCurrentToFirst();
                List.CustomSort = this;
            }

            private void OnCurrentChanged(object sender, System.EventArgs e) {
                Selected = (SpecialEventObject)List.LoadedCurrent;
                FancyBackgroundManager.Instance.ChangeBackground(Selected.PreviewImage);
            }

            public void Unload() { }

            int IComparer.Compare(object x, object y) {
                return AlphanumComparatorFast.Compare((x as AcItemWrapper)?.Id, (y as AcItemWrapper)?.Id);
            }
        }

        private void CarPreview_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var ev = Model.Selected;
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
            var ev = Model.Selected;
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

        private void OnLoaded(object sender, RoutedEventArgs e) {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(AssistsViewModel.Instance).ShowDialog();
        }
    }
}
