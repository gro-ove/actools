using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class PythonSettings : IniPresetableSettings {
            internal PythonSettings() : base(@"python") {}

            private Dictionary<string, bool> _apps;

            public IReadOnlyDictionary<string, bool> Apps => _apps;

            public bool IsActivated([NotNull] string appId) {
                if (appId == null) {
                    throw new ArgumentNullException(nameof(appId));
                }

                bool result;
                return _apps.TryGetValue(appId.ToLowerInvariant(), out result) && result;
            }

            public void SetActivated([NotNull] string appId, bool value) {
                if (appId == null) {
                    throw new ArgumentNullException(nameof(appId));
                }

                _apps[appId.ToLowerInvariant()] = value;
                Save();

                _appsPresets?.InvokeChanged();
            }

            protected override void LoadFromIni() {
                _apps = Ini.Where(x => x.Value.ContainsKey(@"ACTIVE")).ToDictionary(
                        x => x.Key.ToLowerInvariant(),
                        x => x.Value.GetBool("ACTIVE", false));
                OnPropertyChanged(nameof(Apps));
            }

            protected override void SetToIni(IniFile ini) {
                foreach (var app in Apps) {
                    ini[app.Key.ToUpperInvariant()].Set("ACTIVE", app.Value);
                }
            }
        }

        private static PythonSettings _python;

        public static PythonSettings Python => _python ?? (_python = new PythonSettings());
    }
}
