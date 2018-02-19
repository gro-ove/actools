using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsWebBlocks {
        public SettingsWebBlocks() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.WebBlocksSettings WebBlocks => SettingsHolder.WebBlocks;
        }
    }
}
