using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class DefaultSettingEntry : SettingEntry {
        public DefaultSettingEntry(string value, string displayName) : base(value, displayName) { }
        public DefaultSettingEntry(int value, string displayName) : base(value, displayName) { }
    }

    public class SettingEntryStored : Collection<SettingEntry>, INotifyPropertyChanged {
        private readonly StoredValue _stored;

        public SettingEntryStored(string key) {
            _stored = Stored.Get(key);
        }

        private SettingEntry GetDefault() {
            return this.OfType<DefaultSettingEntry>().FirstOrDefault() ?? this.FirstOrDefault();
        }

        private bool _loaded;
        private SettingEntry _selected;

        public SettingEntry SelectedItem {
            get {
                if (!_loaded) {
                    _loaded = true;
                    _selected = this.GetByIdOrDefault(_stored.Value) ?? GetDefault();
                }

                return _selected;
            }
            set {
                if (!Contains(value)) value = GetDefault();
                if (Equals(value, _selected)) return;
                _selected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedValue));
                _stored.Value = value.Id;
            }
        }

        public string SelectedValue {
            get => SelectedItem.Value;
            set => SelectedItem = this.GetByIdOrDefault(value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}