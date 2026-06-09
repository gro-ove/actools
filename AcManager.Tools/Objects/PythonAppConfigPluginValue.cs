using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigPluginValue : PythonAppConfigValue {
        public string RelativePath { get; }

        public string FilterFileName { get; }

        public PythonAppConfigPluginValue(string relativePath, string filterFileName) {
            RelativePath = relativePath;
            FilterFileName = filterFileName;
        }

        public string PluginsDirectory => Path.Combine(FilesRelativeDirectory, RelativePath);

        private void ReloadConfigs() {
            var directory = PluginsDirectory;

            var list = Directory.Exists(directory)
                    ? Directory.GetDirectories(directory)
                            .Where(x => File.Exists(Path.Combine(x, FilterFileName)))
                            .Select(x => {
                                var info = _plugins?.GetByIdOrDefault(Path.GetFileName(directory));
                                if (info != null) {
                                    return info;
                                }
                                return new PythonAppConfigPluginInfo(x);
                            })
                    : new PythonAppConfigPluginInfo[0];
            if (_plugins == null) {
                _plugins = new BetterObservableCollection<PythonAppConfigPluginInfo>(list);
            } else {
                _plugins.ReplaceEverythingBy_Direct(list);
            }
            SelectedPlugin = _plugins.GetByIdOrDefault(Value);
        }

        private BetterObservableCollection<PythonAppConfigPluginInfo> _plugins;

        public BetterObservableCollection<PythonAppConfigPluginInfo> Plugins {
            get {
                if (_plugins == null) {
                    ReloadConfigs();
                }
                return _plugins;
            }
        }

        private PythonAppConfigPluginInfo _selectedPlugin;

        public PythonAppConfigPluginInfo SelectedPlugin {
            get {
                if (_plugins == null) {
                    ReloadConfigs();
                }
                return _selectedPlugin;
            }
            set {
                if (value == null) return;
                Apply(value, ref _selectedPlugin, () => Value = value.Id);
            }
        }

        protected override void OnValueChanged() {
            base.OnValueChanged();
            if (_plugins != null) {
                SelectedPlugin = _plugins.FirstOrDefault(x => x.Id == Value);
            }
        }

        public void ReloadPlugins() {
            if (_plugins == null) return;

            ReloadConfigs();
            SelectedPlugin?.Reload();
        }
    }
}