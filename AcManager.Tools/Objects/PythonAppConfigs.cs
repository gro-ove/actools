using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigParams {
        public PythonAppConfigParams([NotNull] string pythonAppLocation) {
            PythonAppLocation = pythonAppLocation;
            FilesRelativeDirectory = pythonAppLocation;
        }

        [NotNull]
        public string PythonAppLocation { get; }

        [NotNull]
        public string FilesRelativeDirectory { get; set; }

        [CanBeNull]
        public Action DisposalAction { get; set; }

        [CanBeNull]
        public Func<string, IEnumerable<string>> ScanFunc { get; set; }

        [CanBeNull]
        public Func<PythonAppConfigParams, string, PythonAppConfig> ConfigFactory { get; set; }

        public bool SaveOnlyNonDefault { get; set; }

        [CanBeNull]
        public Dictionary<string, string> Flags { get; set; }
    }

    public class PythonAppConfigs : ObservableCollection<PythonAppConfig>, IDisposable {
        public event EventHandler ValueChanged;

        [NotNull]
        public PythonAppConfigParams ConfigParams { get; }

        private static IEnumerable<string> GetSubConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return inis.Length > 10 ? new string[0] : inis;
        }

        private static IEnumerable<string> GetConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return (inis.Length > 10 ? new string[0] : inis).Concat(Directory.GetDirectories(directory).SelectMany(GetSubConfigFiles));
        }

        public PythonAppConfigs([NotNull] PythonAppConfigParams configParams)
                : base((configParams.ScanFunc ?? GetConfigFiles)(configParams.PythonAppLocation)
                        .Select(x => configParams.ConfigFactory != null
                                ? configParams.ConfigFactory(configParams, x)
                                : PythonAppConfig.Create(configParams, x, false))
                        .Where(x => x?.Sections.Any(y => y.Count > 0) == true)
                        .OrderBy(x => x.Order.As(0d)).ThenBy(x => x.DisplayName)) {
            ConfigParams = configParams;
            UpdateReferenced();

            foreach (var config in this) {
                config.ValueChanged += OnValueChanged;
            }
        }

        public bool HandleChanged(string filename) {
            var result = false;
            var updated = false;

            for (var i = Count - 1; i >= 0; i--) {
                var config = this[i];
                if (config.IsAffectedBy(filename)) {
                    if (config.Changed) {
                        result = true;
                    }

                    config.ValueChanged -= OnValueChanged;
                    this[i] = PythonAppConfig.Create(config.ConfigParams, config.Filename, true);
                    this[i].ValueChanged += OnValueChanged;

                    updated = true;
                }
            }

            if (updated) {
                UpdateReferenced();
            }

            return result;
        }

        private void OnValueChanged(object sender, EventArgs e) {
            UpdateReferenced();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private class ValueProvider : IPythonAppConfigValueProvider {
            private readonly ObservableCollection<PythonAppConfig> _root;
            private List<PythonAppConfigSection> _config;
            private Collection<PythonAppConfigValue> _section;
            private Dictionary<string, string> _flags;

            public ValueProvider(PythonAppConfigs root) {
                _root = root;
                _flags = root.ConfigParams.Flags;
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
                if (_flags != null && _flags.TryGetValue(key, out var result)) return result;

                Parse(key, out var param, out var section, out var file);

                var sections = file == null
                        ? _config : _root.FirstOrDefault(x => string.Equals(x.DisplayName, file, StringComparison.OrdinalIgnoreCase))?.Sections;
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

        public void UpdateReferenced() {
            var provider = new ValueProvider(this);
            for (var i = 0; i < Count; i++) {
                var config = this[i];
                provider.SetConfig(config.Sections);

                for (var j = config.Sections.Count - 1; j >= 0; j--) {
                    var section = config.Sections[j];
                    provider.SetSection(section);

                    for (var k = section.Count - 1; k >= 0; k--) {
                        section[k].UpdateReferenced(provider);
                    }
                }
            }
        }

        public void Dispose() {
            ConfigParams.DisposalAction?.Invoke();
        }
    }
}