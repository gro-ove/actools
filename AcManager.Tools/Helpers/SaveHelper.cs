using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public interface ISaveHelper {
        bool IsLoading { get; }

        void Initialize();

        void LoadOrReset();

        void Reset();

        bool Load();

        bool HasSavedData { get; }

        [CanBeNull]
        string ToSerializedString();

        void FromSerializedString([NotNull] string data);

        void FromSerializedStringWithoutSaving([NotNull] string data);

        void Save();

        void SaveLater();

        void RegisterUpgrade<TObsolete>(Func<string, bool> test, Action<TObsolete> load);
    }

    public interface IStringSerializable {
        string Serialize();
    }

    public class SaveHelper<T> : ISaveHelper {
        [CanBeNull]
        private readonly string _key;
        private readonly Func<T> _save;
        private readonly Action<T> _load;
        private readonly Action _reset;
        private readonly Func<string, T> _deserialize;

        public SaveHelper([CanBeNull, Localizable(false)] string key, Func<T> save, Action<T> load, Action reset, Func<string, T> deserialize = null) {
            _key = key;
            _save = save;
            _load = load;
            _reset = reset;
            _deserialize = deserialize;
        }

        private List<IUpgradeEntry> _upgradeEntries;

        private interface IUpgradeEntry {
            bool Test(string serializedData);

            void Load(string serializedData);
        }

        private class UpgradeEntry<TObsolete> : IUpgradeEntry {
            private readonly Func<string, bool> _test;
            private readonly Action<TObsolete> _load;

            public UpgradeEntry(Func<string, bool> test, Action<TObsolete> load) {
                _test = test;
                _load = load;
            }

            public bool Test(string serializedData) {
                return _test(serializedData);
            }

            public void Load(string serializedData) {
                _load(JsonConvert.DeserializeObject<TObsolete>(serializedData));
            }
        }

        public void RegisterUpgrade<TObsolete>(Func<string, bool> test, Action<TObsolete> load) {
            if (_upgradeEntries == null) {
                _upgradeEntries = new List<IUpgradeEntry>(1);
            }
            _upgradeEntries.Add(new UpgradeEntry<TObsolete>(test, load));
        }

        public bool IsLoading { get; private set; }

        public void Initialize() {
            Reset();
            Load();
        }

        public void LoadOrReset() {
            if (!Load()) {
                Reset();
            }
        }

        public void Reset() {
            IsLoading = true;
            _reset();
            IsLoading = false;
        }

        private void LoadInner([NotNull] string data) {
            var obsolete = _upgradeEntries?.FirstOrDefault(x => x.Test(data));
            if (obsolete != null) {
                obsolete.Load(data);
            } else if (_deserialize != null) {
                try {
                    _load(_deserialize(data));
                } catch (Exception e) {
                    Logging.Error(e);
                    _load(JsonConvert.DeserializeObject<T>(data));
                }
            } else {
                _load(JsonConvert.DeserializeObject<T>(data));
            }
        }

        public bool Load() {
            if (_key == null) return false;

            var data = ValuesStorage.GetString(_key);
            if (data == null) return false;

            try {
                IsLoading = true;
                LoadInner(data);
                return true;
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
            } finally {
                IsLoading = false;
            }

            return false;
        }

        private string Serialize<TAny>(TAny obj) {
            return (obj as IStringSerializable)?.Serialize() ?? JsonConvert.SerializeObject(obj);
        }

        public bool HasSavedData => _key != null && ValuesStorage.Contains(_key);

        public string ToSerializedString() {
            var obj = _save();
            return obj == null ? null : Serialize(obj);
        }

        public void FromSerializedString([NotNull] string data, bool disableSaving) {
            try {
                IsLoading = disableSaving;
                LoadInner(data);
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
            } finally {
                IsLoading = false;
            }
        }

        public void FromSerializedString(string data) {
            FromSerializedString(data, false);
        }

        public void FromSerializedStringWithoutSaving(string data) {
            FromSerializedString(data, true);
        }

        public void Save() {
            if (_key == null) return;

            var serialized = ToSerializedString();
            if (serialized == null) return;
            ValuesStorage.Set(_key, serialized);
        }

        private bool _savingInProgress;

        public async void SaveLater() {
            if (IsLoading || _savingInProgress || _key == null) return;
            _savingInProgress = true;

            await Task.Delay(300);

            if (IsLoading) {
                return;
            }
            
            Save();
            _savingInProgress = false;
        }
    }
}
