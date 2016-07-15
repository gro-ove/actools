using System.Collections.ObjectModel;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsPlugins {
        public SettingsPlugins() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                PluginsManager.Instance.UpdateList();
            }

            public ObservableCollection<PluginEntry> List => PluginsManager.Instance.List;
        }
    }
}
