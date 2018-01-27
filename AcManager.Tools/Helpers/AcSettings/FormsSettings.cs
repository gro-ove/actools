using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public class FormsSettings : IniPresetableSettings {
        internal FormsSettings() : base(@"acos") {}

        private AcFormEntry[] _entries;

        public AcFormEntry[] Entries {
            get => _entries;
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();
            }
        }

        private int _selectedDesktop;

        public int SelectedDesktop {
            get => _selectedDesktop;
            set {
                value = value.Clamp(1, 4);
                if (Equals(value, _selectedDesktop)) return;
                _selectedDesktop = value;
                OnPropertyChanged();
            }
        }

        public static List<AcFormEntry> Load(IniFile ini) {
            var list = new List<AcFormEntry>();

            int GetDesktop(string sectionId) {
                return sectionId.StartsWith("DESK_") ? sectionId.Substring(5).Split('_')[0].As<int>() : 0;
            }

            string GetId(string sectionId) {
                var i = sectionId.IndexOf("FORM_", StringComparison.Ordinal);
                return i == -1 ? sectionId : sectionId.Substring(i + 5);
            }

            void AddOrExtend(int desktop, string id, IniFileSection section) {
                var existing = list.GetByIdOrDefault(id);
                if (existing == null) {
                    list.Add(new AcFormEntry(id, desktop, section));
                } else {
                    existing.Extend(desktop, section);
                }
            }

            foreach (var s in ini) {
                if (s.Key.StartsWith("FORM_") || s.Key.StartsWith("DESK_")) {
                    AddOrExtend(GetDesktop(s.Key), GetId(s.Key), s.Value);
                }
            }

            return list;
        }

        protected override void Replace(IniFile ini, bool backup = false) {
            if (ini.Keys.Any(x => x.StartsWith("FORM_"))) {
                Logging.Warning("Obsolete format! Resaving…");
                var entries = Load(ini);

                foreach (var v in ini.Keys.Where(x => x.StartsWith("FORM_")).ToList()) {
                    ini.Remove(v);
                }

                foreach (var entry in entries) {
                    entry.SaveTo(ini);
                }

                if (ini["HEADER"].GetInt("VERSION", 1) < 2) {
                    ini["HEADER"].Set("VERSION", 2);
                    ini["HEADER"].Set("DESKTOP_SELECTED", 1);
                }
            }

            base.Replace(ini, backup);
        }

        protected override void LoadFromIni() {
            Entries = Load(Ini).ToArray();
            Array.Sort(Entries, (l, r) => AlphanumComparatorFast.Compare(l.DisplayName, r.DisplayName));

            foreach (var entry in Entries.SelectMany(x => x.Desktops)) {
                entry.PropertyChanged += OnEntryPropertyChanged;
            }

            if (Ini.Keys.Any(x => x.StartsWith("FORM_"))) {
                Fix().Forget();
            }

            SelectedDesktop = Ini["HEADER"].GetInt("DESKTOP_SELECTED", 1);
        }

        private async Task Fix() {
            Logging.Warning("Obsolete format! Resaving…");
            await Task.Delay(1);
            Save();
        }

        private void OnEntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            Save();
        }

        protected override void SetToIni(IniFile ini) {
            foreach (var entry in Entries) {
                entry.SaveTo(ini);
            }

            if (ini["HEADER"].GetInt("VERSION", 1) < 2) {
                ini["HEADER"].Set("VERSION", 2);
            }

            ini["HEADER"].Set("DESKTOP_SELECTED", SelectedDesktop);
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.AppsPresetChanged();
        }

        public static IniFile Combine([ItemCanBeNull] IEnumerable<Tuple<IniFile, int>> ini, int selectedDesktop) {
            var result = new IniFile();
            result["HEADER"].Set("VERSION", 2);
            result["HEADER"].Set("DESKTOP_SELECTED", selectedDesktop.Clamp(1, 4));

            var forms = ini.Select(x => x == null ? null
                    : Load(x.Item1).Select(y => new { y.Id, DesktopForm = y.Desktops[x.Item2] }).Where(y => y.DesktopForm.IsVisible)).ToList();
            foreach (var id in forms.SelectMany(x => x.Select(y => y.Id)).Distinct()) {
                var formEntry = new AcFormEntry(id);

                for (var i = 0; i < forms.Count; i++) {
                    var form = forms[i]?.FirstOrDefault(x => x.Id == id);
                    if (form == null) continue;

                    formEntry.Desktops[i].CopyFrom(form.DesktopForm);
                }

                formEntry.SaveTo(result);
            }

            return result;
        }
    }
}