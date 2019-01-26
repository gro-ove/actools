using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public class PythonSettings : IniPresetableSettings {
        internal PythonSettings() : base("python") { }

        private Dictionary<string, bool> _apps;
        private bool _fullyLoaded;

        public IReadOnlyDictionary<string, bool> Apps => _apps;

        public bool IsActivated([NotNull] string appId) {
            if (appId == null) {
                throw new ArgumentNullException(nameof(appId));
            }

            return _apps.TryGetValue(appId.ToLowerInvariant(), out var result) && result;
        }

        public void SetActivated([NotNull] string appId, bool value) {
            if (appId == null) {
                throw new ArgumentNullException(nameof(appId));
            }

            _apps[appId.ToLowerInvariant()] = value;
            Save();

            AppActiveStateChanged?.Invoke(this, EventArgs.Empty);

            if (_fullyLoaded) {
                PythonAppsManager.Instance.GetById(appId)?.OnActiveChanged();
            }
        }

        public event EventHandler AppActiveStateChanged;

        protected override void LoadFromIni() {
            _apps = Ini.Where(x => x.Value.ContainsKey(@"ACTIVE")).ToDictionary(
                    x => x.Key.ToLowerInvariant(),
                    x => x.Value.GetBool("ACTIVE", false));
            OnPropertyChanged(nameof(Apps));
            AppActiveStateChanged?.Invoke(this, EventArgs.Empty);
            if (_fullyLoaded) {
                foreach (var app in PythonAppsManager.Instance.Loaded) {
                    app?.OnActiveChanged();
                }
            }
            _fullyLoaded = true;
        }

        protected override void SetToIni(IniFile ini) {
            foreach (var app in Apps) {
                ini[app.Key.ToUpperInvariant()].Set("ACTIVE", app.Value);
            }
        }

        public static IniFile Combine([ItemNotNull] IEnumerable<IniFile> ini) {
            var list = ini.ToList();
            var result = new IniFile();
            foreach (var k in list.SelectMany(x => x.Where(y => y.Value.ContainsKey("ACTIVE")).Select(y => y.Key)).Distinct()) {
                result[k].Set("ACTIVE", list.Any(x => x[k].GetBool("ACTIVE", false)));
            }
            return result;
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.AppsPresetChanged();
        }
    }
}