using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsGeneral {
        public AcSettingsGeneral() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public ExposureSettings Exposure => AcSettingsHolder.Exposure;

            public ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
