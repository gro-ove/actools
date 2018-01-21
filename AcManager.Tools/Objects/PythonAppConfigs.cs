using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigs : ObservableCollection<PythonAppConfig>, IDisposable {
        public event EventHandler ValueChanged;

        private readonly Action _disposalAction;

        private static IEnumerable<string> GetSubConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return inis.Length > 10 ? new string[0] : inis;
        }

        private static IEnumerable<string> GetConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return (inis.Length > 10 ? new string[0] : inis).Concat(Directory.GetDirectories(directory).SelectMany(GetSubConfigFiles));
        }

        public PythonAppConfigs(string location, Action disposalAction) : base(GetConfigFiles(location)
                .Select(file => PythonAppConfig.Create(file, location, false)).Where(x => x?.Sections.Any(y => y.Count > 0) == true)) {
            _disposalAction = disposalAction;
            UpdateEnabled();

            foreach (var config in this) {
                config.ValueChanged += OnValueChanged;
            }
        }

        public bool HandleChanged(string location, string filename) {
            var result = false;
            var updated = false;

            for (var i = Count - 1; i >= 0; i--) {
                var config = this[i];
                if (config.IsAffectedBy(filename)) {
                    if (config.Changed) {
                        result = true;
                    }

                    config.ValueChanged -= OnValueChanged;
                    this[i] = PythonAppConfig.Create(config.Filename, location, true);
                    this[i].ValueChanged += OnValueChanged;

                    updated = true;
                }
            }

            if (updated) {
                UpdateEnabled();
            }

            return result;
        }

        private void OnValueChanged(object sender, EventArgs e) {
            UpdateEnabled();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private class ValueProvider : IPythonAppConfigValueProvider {
            private readonly ObservableCollection<PythonAppConfig> _root;
            private List<PythonAppConfigSection> _config;
            private Collection<PythonAppConfigValue> _section;

            public ValueProvider(ObservableCollection<PythonAppConfig> root) {
                _root = root;
            }

            private static int LastIndexOf(string key) {
                int i0 = key.LastIndexOf('/'), i1 = key.LastIndexOf('\\');
                return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
            }

            private static int LastIndexOf(string key, int from) {
                int i0 = key.LastIndexOf('/', from), i1 = key.LastIndexOf('\\', from);
                return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
            }

            private static void Parse(string key, out string param, out string section, out string file) {
                var paramSep = LastIndexOf(key);
                if (paramSep <= 0) {
                    param = paramSep == -1 ? key : key.Substring(1);
                    section = file = null;
                    return;
                }

                param = key.Substring(paramSep + 1);

                var sectionSep = LastIndexOf(key, paramSep - 1);
                if (sectionSep <= 0) {
                    section = sectionSep == -1 ? key.Substring(0, paramSep) : key.Substring(1, paramSep - 1);
                    file = null;
                    return;
                }

                section = key.Substring(sectionSep + 1, paramSep - sectionSep - 1);
                file = key.Substring(0, sectionSep);
            }

            public string Get(string key) {
                Parse(key, out var param, out var section, out var file);

                var sections = file == null ? _config : _root.FirstOrDefault(x => string.Equals(x.DisplayName, file, StringComparison.OrdinalIgnoreCase))?.Sections;
                if (sections == null) return null;

                var values = section == null ? _section : sections.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase));
                return values?.FirstOrDefault(x => string.Equals(x.OriginalKey, param))?.Value;
            }

            public void SetConfig(List<PythonAppConfigSection> config) {
                _config = config;
            }

            public void SetSection(Collection<PythonAppConfigValue> section) {
                _section = section;
            }
        }

        public void UpdateEnabled() {
            var provider = new ValueProvider(this);
            for (var i = 0; i < Count; i++) {
                var config = this[i];
                provider.SetConfig(config.Sections);

                for (var j = config.Sections.Count - 1; j >= 0; j--) {
                    var section = config.Sections[j];
                    provider.SetSection(section);

                    for (var k = section.Count - 1; k >= 0; k--) {
                        var value = section[k];
                        if (value.IsEnabledTest != null) {
                            value.IsEnabled = value.IsEnabledTest(provider);
                        }
                    }
                }
            }
        }

        public void Dispose() {
            _disposalAction?.Invoke();
        }
    }
}