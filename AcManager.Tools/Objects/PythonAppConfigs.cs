using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigs : ChangeableObservableCollection<PythonAppConfig>, IDisposable {
        public event EventHandler<ValueChangedEventArgs> ValueChanged;

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
                                : new []{ PythonAppConfig.Create(configParams, x, false) })
                        .NonNull()
                        .SelectMany(x => x)
                        .Where(x => x?.SectionsOwn.Any(y => y.Count > 0) == true)
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

        private void OnValueChanged(object sender, ValueChangedEventArgs e) {
            UpdateReferenced();
            ValueChanged?.Invoke(this, e);
        }

        private void UpdateReferenced() {
            for (var i = 0; i < Count; i++) {
                this[i].UpdateReferenced();
            }
        }

        public void Dispose() {
            ConfigParams.DisposalAction?.Invoke();
            foreach (var config in Items) {
                config.Dispose();
            }
        }
    }
}