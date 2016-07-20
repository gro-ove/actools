using System;
using System.ComponentModel;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public interface ISaveHelper {
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
        private readonly string _key;
        private readonly Func<T> _save;
        private readonly Action<T> _load;
        private readonly Action _reset;

        public SaveHelper([Localizable(false)] string key, Func<T> save, Action<T> load, Action reset) {
            _key = key;
            _save = save;
            _load = load;
            _reset = reset;
        }

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
            _disableSaving = true;
            _reset();
            _disableSaving = false;
        }

        public bool Load() {
            var data = ValuesStorage.GetString(_key);
            if (data == null) return false;

            try {
                _disableSaving = true;
                _load(JsonConvert.DeserializeObject<T>(data));
                return true;
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
            } finally {
                _disableSaving = false;
            }

            return false;
        }

        public bool HasSavedData => ValuesStorage.Contains(_key);

        public string ToSerializedString() {
            var obj = _save();
            return obj == null ? null : JsonConvert.SerializeObject(obj);
        }

        public void FromSerializedString(string data, bool disableSaving) {
            try {
                _disableSaving = disableSaving;
                _load(JsonConvert.DeserializeObject<T>(data));
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
            } finally {
                _disableSaving = false;
            }
        }

        public void FromSerializedString(string data) {
            FromSerializedString(data, false);
        }

        public void FromSerializedStringWithoutSaving(string data) {
            FromSerializedString(data, true);
        }

        public void Save() {
            var serialized = ToSerializedString();
            if (serialized == null) return;
            ValuesStorage.Set(_key, serialized);
        }

        private bool _disableSaving, _savingInProgress;

        public async void SaveLater() {
            if (_disableSaving || _savingInProgress) return;
            _savingInProgress = true;

            await Task.Delay(300);

            if (_disableSaving) {
                return;
            }
            
            Save();
            _savingInProgress = false;
        }
    }
}
