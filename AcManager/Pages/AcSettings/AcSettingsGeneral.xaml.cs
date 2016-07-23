using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsGeneral {
        public AcSettingsGeneral() {
            InitializeComponent();
            DataContext = new AcGeneralViewModel();
        }

        public class AcGeneralViewModel : NotifyPropertyChanged {
            internal AcGeneralViewModel() { }

            public ExposureSettings Exposure => AcSettingsHolder.Exposure;

            public ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
