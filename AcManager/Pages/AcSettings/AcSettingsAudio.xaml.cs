using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsAudio {
        public AcSettingsAudio() {
            InitializeComponent();
            DataContext = new AcAudioViewModel();
        }

        public class AcAudioViewModel : NotifyPropertyChanged {
            internal AcAudioViewModel() { }

            public AcSettingsHolder.AudioSettings Audio => AcSettingsHolder.Audio;
        }
    }
}
