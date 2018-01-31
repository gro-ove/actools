using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public class Stored : Binding {
        public Stored() {
            Key = "";
        }

        public Stored(string path) : base(path) {
            Key = path;
        }

        public Stored(string key, object defaultValue) : base(key) {
            _key = key;
            Initialize(_key, defaultValue);
        }

        private string _key;

        public string Key {
            get => _key;
            set {
                if (Equals(_key, value)) return;

                var i = value.IndexOf('=');
                string defaultValue;
                if (i != -1) {
                    defaultValue = value.Substring(i + 1);
                    _key = value.Substring(0, i);
                } else {
                    defaultValue = null;
                    _key = value;
                }

                Initialize(_key, defaultValue);
            }
        }

        public static StoredValue Get(string key, object defaultValue = null) {
            return StoredValue.Create(key, defaultValue);
        }

        public static StoredValue<T> Get<T>(string key, T defaultValue = default(T)) {
            return StoredValue.Create(key, null).GetStrict(defaultValue);
        }

        public static string GetValue(string key, object defaultValue = null) {
            return StoredValue.Create(key, defaultValue).Value;
        }

        public static T GetValue<T>(string key, T defaultValue = default(T)) {
            return StoredValue.Create(key, defaultValue).Value.As(defaultValue);
        }

        public static void SetValue(string key, string value) {
            StoredValue.Create(key, null).Value = value;
        }

        private void Initialize(string key, object defaultValue) {
            Source = StoredValue.Create(key, defaultValue);
            Path = new PropertyPath(nameof(StoredValue.Value));
            Mode = BindingMode.TwoWay;
        }
    }
}