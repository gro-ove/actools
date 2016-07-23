using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsAudio {
        public AcSettingsAudio() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public AudioSettings Audio => AcSettingsHolder.Audio;
        }
    }
}
