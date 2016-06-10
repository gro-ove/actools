using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class PythonSettings : IniSettings {
            internal PythonSettings() : base("python") {}

            private Dictionary<string, bool> _apps;

            public Dictionary<string, bool> Apps {
                get { return _apps; }
                set {
                    if (Equals(value, _apps)) return;
                    _apps = value;
                    OnPropertyChanged();
                }
            }

            public bool IsActivated([NotNull] string appId) {
                if (appId == null) {
                    throw new ArgumentNullException(nameof(appId));
                }
                
                return Apps.GetValueOr(appId.ToLowerInvariant(), false);
            }

            public void SetActivated([NotNull] string appId, bool value) {
                if (appId == null) {
                    throw new ArgumentNullException(nameof(appId));
                }

                Apps[appId] = value;
                Save();
            }

            protected override void LoadFromIni() {
                Apps = Ini.Where(x => x.Value.ContainsKey("ACTIVE")).ToDictionary(
                        x => x.Key.ToLowerInvariant(),
                        x => x.Value.GetBool("ACTIVE", false));
            }

            protected override void SetToIni() {
                foreach (var app in Apps) {
                    Ini[app.Key.ToUpperInvariant()].Set("ACTIVE", app.Value);
                }
            }
        }

        private static PythonSettings _python;

        public static PythonSettings Python => _python ?? (_python = new PythonSettings());
    }
}
