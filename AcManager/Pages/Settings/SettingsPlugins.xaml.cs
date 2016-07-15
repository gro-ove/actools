using System;
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
            private static DateTime _lastUpdated;

            public ViewModel() {
                if (DateTime.Now - _lastUpdated < TimeSpan.FromMinutes(1d)) return;

                _lastUpdated = DateTime.Now;
                PluginsManager.Instance.UpdateList().Forget();
            }

            public ObservableCollection<PluginEntry> List => PluginsManager.Instance.List;
        }
    }
}
