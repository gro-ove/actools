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
        [CanBeNull]
        string Key { get; }

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

        bool SaveLater();

        void RegisterUpgrade<TObsolete>(Func<string, bool> test, Action<TObsolete> load);
    }

    public class SaveHelper<T> : ISaveHelper where T : class, new() {
        private readonly Func<T> _save;
        private readonly Action<T> _load;
        private readonly Action _reset;
        private readonly Func<string, T> _deserialize;

        [NotNull]
        private readonly IStorage _storage;

        public string Key { get; }

        public SaveHelper([CanBeNull, Localizable(false)] string key, Func<T> save, Action<T> load, Action reset, Func<string, T> deserialize = null,
                IStorage storage = null) {
            Key = key;
            _save = save;
            _load = load;
            _reset = reset;
            _deserialize = deserialize;
            _storage = storage ?? ValuesStorage.Storage;
        }

        public SaveHelper([CanBeNull, Localizable(false)] string key, Func<T> save, Action<T> load, Func<string, T> deserialize = null,
                IStorage storage = null) {
            Key = key;
            _save = save;
            _load = load;
            _reset = () => _load(new T());
            _deserialize = deserialize;
            _storage = storage ?? ValuesStorage.Storage;
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
            if (Key == null) return false;

            var data = _storage.GetString(Key);
            if (data == null) return false;

            try {
                IsLoading = true;
                LoadInner(data);
                return true;
            } catch (Exception e) {
                Logging.Error(e);
            } finally {
                IsLoading = false;
            }

            return false;
        }

        public bool HasSavedData => Key != null && _storage.Contains(Key);
        
        public string ToSerializedString() {
            var obj = _save();
            return obj == null ? null : Serialize(obj);
        }

        public void FromSerializedString([NotNull] string data, bool disableSaving) {
            try {
                IsLoading = disableSaving;
                LoadInner(data);
            } catch (Exception e) {
                Logging.Error(data);
                Logging.Error(e);
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
            var key = Key;
            if (key == null) return;

            var serialized = ToSerializedString();
            if (serialized == null) return;
            _storage.Set(key, serialized);
        }

        private bool _savingInProgress;

        public bool SaveLater() {
            if (IsLoading || _savingInProgress) return false;
            SaveLaterAsync().Forget();
            return true;
        }

        private async Task SaveLaterAsync() {
            if (IsLoading || _savingInProgress || Key == null) return;
            _savingInProgress = true;

            await Task.Delay(1000);

            if (IsLoading) {
                return;
            }
            
            Save();
            _savingInProgress = false;
        }

        [CanBeNull, Pure]
        public static T Load([NotNull] string key, IStorage storage = null) {
            return LoadSerialized((storage ?? ValuesStorage.Storage).GetString(key));
        }

        [NotNull, Pure]
        public static T LoadOrReset([NotNull] string key, IStorage storage = null) {
            return LoadSerialized((storage ?? ValuesStorage.Storage).GetString(key)) ?? new T();
        }

        [CanBeNull, Pure]
        public static T LoadSerialized([CanBeNull] string data) {
            if (data != null) {
                try {
                    return JsonConvert.DeserializeObject<T>(data);
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            return null;
        }

        [CanBeNull, Pure]
        public static string Serialize(T obj) {
            return (obj as IJsonSerializable)?.ToJson() ?? JsonConvert.SerializeObject(obj);
        }
    }
}
