using System.Collections.ObjectModel;
using AcManager.Tools.Helpers;
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
                PluginsManager.Instance.UpdateIfObsolete().Forget();
            }

            public ObservableCollection<PluginEntry> List => PluginsManager.Instance.List;
        }
    }
}
