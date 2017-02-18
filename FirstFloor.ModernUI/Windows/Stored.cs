using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows {
    public class Stored : Binding {
        public Stored() {
            Initialize(@"_stored");
        }

        public Stored(string path) : base(path) {
            Initialize(@"_stored:" + path);
        }

        private class SourceInner : NotifyPropertyChanged {
            private readonly string _key;

            public SourceInner(string key) {
                _key = key;
            }

            private string _value;

            public string Value {
                get { return _value ?? (_value = ValuesStorage.GetString(_key)); }
                set {
                    if (Equals(value, Value)) return;
                    _value = value;
                    ValuesStorage.Set(_key, value);
                    OnPropertyChanged();
                }
            }
        }

        private void Initialize(string key) {
            Source = new SourceInner(key);
            Path = new PropertyPath(nameof(SourceInner.Value));
            Mode = BindingMode.TwoWay;
        }
    }
}