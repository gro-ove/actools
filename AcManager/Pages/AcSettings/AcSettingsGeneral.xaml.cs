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

            public AcSettingsHolder.ExposureSettings Exposure => AcSettingsHolder.Exposure;

            public AcSettingsHolder.ReplaySettings Replay => AcSettingsHolder.Replay;
        }
    }
}
