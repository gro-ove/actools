using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class DefaultSettingEntry : SettingEntry {
        public DefaultSettingEntry([Localizable(false)] string value, string displayName) : base(value, displayName) { }
        public DefaultSettingEntry(int value, string displayName) : base(value, displayName) { }
    }

    public class SettingEntryStored : Collection<SettingEntry>, INotifyPropertyChanged {
        [NotNull]
        private readonly string _key;

        [CanBeNull]
        private StoredValue _stored;

        public SettingEntryStored(string key) {
            _key = key;
        }

        public SettingEntryStored([Localizable(false)] string key, Action<SettingEntry> change) {
            _key = key;
            _change = change;
        }

        private SettingEntry GetDefault() {
            return this.OfType<DefaultSettingEntry>().FirstOrDefault() ?? this.FirstOrDefault();
        }

        private bool _loaded;
        private SettingEntry _selected;

        public SettingEntry SelectedItem {
            get {
                var stored = _stored ?? (_stored = Stored.Get(_key));

                if (!_loaded) {
                    _loaded = true;
                    _selected = this.GetByIdOrDefault(stored.Value) ?? GetDefault();
                }

                return _selected;
            }
            set {
                if (!Contains(value)) value = GetDefault();
                if (Equals(value, SelectedItem)) return;
                _selected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedValue));

                var stored = _stored ?? (_stored = Stored.Get(_key));
                stored.Value = value.Id;

                _change?.Invoke(value);
            }
        }

        public string SelectedValue {
            get => SelectedItem.Value;
            set => SelectedItem = this.GetByIdOrDefault(value);
        }

        [CanBeNull]
        private readonly Action<SettingEntry> _change;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}