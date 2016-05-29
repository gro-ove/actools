using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsGeneral {
        public AcSettingsGeneral() {
            InitializeComponent();
            DataContext = new AcGeneralViewModel();
        }

        public class AcGeneralViewModel : NotifyPropertyChanged {
            internal AcGeneralViewModel() { }

            public AcSettingsHolder.CameraOnboardSettings CameraOnboard => AcSettingsHolder.CameraOnboard;

            public AcSettingsHolder.GameplaySettings Gameplay => AcSettingsHolder.Gameplay;

            public AcSettingsHolder.VideoSettings Video => AcSettingsHolder.Video;

            public AcSettingsHolder.ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
