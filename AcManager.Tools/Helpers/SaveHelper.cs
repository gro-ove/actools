using System;
using System.ComponentModel;
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

        string ToSerializedString();

        void FromSerializedString(string data);

        void FromSerializedStringWithoutSaving(string data);

        void Save();

        void SaveLater();
    }


    public class SaveHelper<T> : ISaveHelper {
        [CanBeNull]
        private readonly string _key;
        private readonly Func<T> _save;
        private readonly Action<T> _load;
        private readonly Action _reset;

        public SaveHelper([CanBeNull, Localizable(false)] string key, Func<T> save, Action<T> load, Action reset) {
            _key = key;
            _save = save;
            _load = load;
            _reset = reset;
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

        public bool Load() {
            if (_key == null) return false;

            var data = ValuesStorage.GetString(_key);
            if (data == null) return false;

            try {
                IsLoading = true;
                _load(JsonConvert.DeserializeObject<T>(data));
                return true;
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
            } finally {
                IsLoading = false;
            }

            return false;
        }

        public bool HasSavedData => _key != null && ValuesStorage.Contains(_key);

        public string ToSerializedString() {
            var obj = _save();
            return obj == null ? null : JsonConvert.SerializeObject(obj);
        }

        public void FromSerializedString(string data, bool disableSaving) {
            try {
                IsLoading = disableSaving;
                _load(JsonConvert.DeserializeObject<T>(data));
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
