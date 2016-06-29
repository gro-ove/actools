using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public sealed class AcFormEntry : Displayable {
        public string Id { get; }

        public AcFormEntry(string id) {
            Id = id;
            DisplayName = id.ApartFromFirst("FORM_");
        }

        private int _posX;

        public int PosX {
            get { return _posX; }
            set {
                if (Equals(value, _posX)) return;
                _posX = value;
                OnPropertyChanged();
            }
        }

        private int _posY;

        public int PosY {
            get { return _posY; }
            set {
                if (Equals(value, _posY)) return;
                _posY = value;
                OnPropertyChanged();
            }
        }

        private bool _isVisible;

        public bool IsVisible {
            get { return _isVisible; }
            set {
                if (Equals(value, _isVisible)) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isBlocked;

        public bool IsBlocked {
            get { return _isBlocked; }
            set {
                if (Equals(value, _isBlocked)) return;
                _isBlocked = value;
                OnPropertyChanged();
            }
        }

        private int _scale;

        public int Scale {
            get { return _scale; }
            set {
                value = value.Clamp(0, 10000);
                if (Equals(value, _scale)) return;
                _scale = value;
                OnPropertyChanged();
            }
        }
    }

    public partial class AcSettingsHolder {
        public abstract class IniPresetableSettings : IniSettings {
            protected IniPresetableSettings(string name, bool reload = true, bool systemConfig = false) : base(name, reload, systemConfig) { }

            public void Import(string serialized) {
                if (serialized == null) return;
                Ini = IniFile.Parse(serialized);
                LoadFromIni();
                Save();
            }

            public string Export() {
                var ini = new IniFile();
                SetToIni(ini);
                return ini.Stringify();
            }

            protected override void SetToIni() {
                SetToIni(Ini);
            }

            protected abstract void SetToIni(IniFile ini);
        }

        public class FormsSettings : IniPresetableSettings {
            internal FormsSettings() : base("acos") {}

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
                Entries = Ini.Where(x => x.Key.StartsWith("FORM_")).Select(x => new AcFormEntry(x.Key) {
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
                _appsPresets?.InvokeChanged();
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
        }

        private static FormsSettings _forms;

        public static FormsSettings Forms => _forms ?? (_forms = new FormsSettings());

        private class AppsPresetsInner : IUserPresetable {
            private class Saveable {
                public string PythonData, FormsData;
            }

            public bool CanBeSaved => true;

            public string PresetableKey => "In-Game Apps";

            string IUserPresetable.PresetableCategory => PresetableKey;

            string IUserPresetable.DefaultPreset => null;

            public string ExportToPresetData() {
                return JsonConvert.SerializeObject(new Saveable {
                    PythonData = Python.Export(),
                    FormsData = Forms.Export()
                });
            }

            public event EventHandler Changed;

            public void InvokeChanged() {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public void ImportFromPresetData(string data) {
                var entry = JsonConvert.DeserializeObject<Saveable>(data);
                Python.Import(entry.PythonData);
                Forms.Import(entry.FormsData);
            }
        }

        private static AppsPresetsInner _appsPresets;

        public static IUserPresetable AppsPresets => _appsPresets ?? (_appsPresets = new AppsPresetsInner());
    }
}
