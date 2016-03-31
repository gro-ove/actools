using System.Collections.ObjectModel;
using System.ComponentModel;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Addons;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsAddons {
        public SettingsAddons() {
            InitializeComponent();
            DataContext = new AddonsViewModel();
        }

        public class AddonsViewModel
            : NotifyPropertyChanged {
            public AddonsViewModel (){
                AppAddonsManager.Instance.UpdateList();
            }

            public ObservableCollection<AppAddonInformation> List => AppAddonsManager.Instance.List;
        }
    }
}
