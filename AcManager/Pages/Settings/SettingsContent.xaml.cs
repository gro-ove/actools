using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsContent {
        public SettingsContent() {
            InitializeComponent();
            DataContext = new ViewModel();
        }


        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.ContentSettings Holder => SettingsHolder.Content;

            internal ViewModel() {}
        }
    }
}
