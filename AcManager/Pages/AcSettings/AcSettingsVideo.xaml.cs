using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo: ILoadableContent {
        public class AcVideoViewModel : NotifyPropertyChanged {
            internal AcVideoViewModel() {}

            public AcSettingsHolder.VideoSettings Video => AcSettingsHolder.Video;

            public AcSettingsHolder.OculusSettings Oculus => AcSettingsHolder.Oculus;

            public AcSettingsHolder.GraphicsSettings Graphics => AcSettingsHolder.Graphics;

            public IUserPresetable Presets => AcSettingsHolder.VideoPresets;

            private RelayCommand _manageFiltersCommand;

            public RelayCommand ManageFiltersCommand => _manageFiltersCommand ?? (_manageFiltersCommand = new RelayCommand(o => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/PpFiltersListPage.xaml", UriKind.RelativeOrAbsolute));
            }, o => AppKeyHolder.IsAllRight));
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return PpFiltersManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            PpFiltersManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new AcVideoViewModel();
        }
    }
}
