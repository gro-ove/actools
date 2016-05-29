using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo {
        public AcSettingsVideo() {
            InitializeComponent();
            DataContext = new AcVideoViewModel();
        }

        public class AcVideoViewModel : NotifyPropertyChanged {
            internal AcVideoViewModel() { }

            public AcSettingsHolder.VideoSettings Video => AcSettingsHolder.Video;
        }
    }
}
