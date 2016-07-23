using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class FormsSettings : IniPresetableSettings {
        internal FormsSettings() : base(@"acos") {}

        private AcFormEntry[] _entries;

        public AcFormEntry[] Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            Entries = Ini.Where(x => x.Key.StartsWith(@"FORM_")).Select(x => new AcFormEntry(x.Key) {
                PosX = x.Value.GetInt("POSX", 0),
                PosY = x.Value.GetInt("POSY", 0),
                IsVisible = x.Value.GetBool("VISIBLE", false),
                IsBlocked = x.Value.GetBool("BLOCKED", false),
                Scale = x.Value.GetDouble("SCALE", 1d).ToIntPercentage(),
            }).ToArray();

            foreach (var entry in Entries) {
                entry.PropertyChanged += Entry_PropertyChanged;
            }
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            Save();
        }

        protected override void SetToIni(IniFile ini) {
            foreach (var entry in Entries) {
                var section = ini[entry.Id];
                section.Set("POSX", entry.PosX);
                section.Set("POSY", entry.PosY);
                section.Set("VISIBLE", entry.IsVisible);
                section.Set("BLOCKED", entry.IsBlocked);
                section.Set("SCALE", entry.Scale.ToDoublePercentage());
            }
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.AppsPresetChanged();
        }
    }
}