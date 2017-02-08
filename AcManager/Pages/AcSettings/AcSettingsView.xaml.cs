using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsView {
        public AcSettingsView() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public CameraManagerSettings CameraManager => AcSettingsHolder.CameraManager;

            public CameraOnboardSettings CameraOnboard => AcSettingsHolder.CameraOnboard;

            public GameplaySettings Gameplay => AcSettingsHolder.Gameplay;

            public VideoSettings Video => AcSettingsHolder.Video;

            public ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
