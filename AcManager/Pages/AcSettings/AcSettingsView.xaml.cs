using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsView {
        public AcSettingsView() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public CameraManagerSettings CameraManager => AcSettingsHolder.CameraManager;
            public CameraOnboardSettings CameraOnboard => AcSettingsHolder.CameraOnboard;
            public GameplaySettings Gameplay => AcSettingsHolder.Gameplay;
            public VideoSettings Video => AcSettingsHolder.Video;
            public ReplaySettings Replay => AcSettingsHolder.Replay;
            public MessagesSettings Messages => AcSettingsHolder.Messages;
        }
    }
}
