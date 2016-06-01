using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsView {
        public AcSettingsView() {
            InitializeComponent();
            DataContext = new AcViewViewModel();
        }

        public class AcViewViewModel : NotifyPropertyChanged {
            internal AcViewViewModel() { }

            public AcSettingsHolder.CameraOnboardSettings CameraOnboard => AcSettingsHolder.CameraOnboard;

            public AcSettingsHolder.GameplaySettings Gameplay => AcSettingsHolder.Gameplay;

            public AcSettingsHolder.VideoSettings Video => AcSettingsHolder.Video;

            public AcSettingsHolder.ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
