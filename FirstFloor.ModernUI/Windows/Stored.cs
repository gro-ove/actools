using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public class Stored : Binding {
        public Stored() {
            Key = "";
        }

        public Stored(string path) : base(path) {
            Key = path;
        }

        private string _key;

        public string Key {
            get { return _key; }
            set {
                if (Equals(_key, value)) return;

                _key = value;
                var i = value.IndexOf('=');
                string defaultValue;
                if (i != -1) {
                    defaultValue = value.Substring(i + 1);
                    value = value.Substring(0, i);
                } else {
                    defaultValue = null;
                }

                Initialize(@"_stored:" + value, defaultValue);
            }
        }

        public static StoredValue Get(string key, string defaultValue = null) {
            return StoredValue.Create(key, defaultValue);
        }

        public class StoredValue : NotifyPropertyChanged {
            private static readonly Dictionary<string, WeakReference<StoredValue>> Instances = new Dictionary<string, WeakReference<StoredValue>>();

            private static void RemoveDeadReferences<TKey, TValue>([NotNull] IDictionary<TKey, WeakReference<TValue>> dictionary) where TValue : class {
                if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

                TValue temp;
                var toRemove = dictionary.Where(x => !x.Value.TryGetTarget(out temp)).Select(x => x.Key).ToList();

                foreach (var key in toRemove) {
                    dictionary.Remove(key);
                }
            }

            internal static StoredValue Create(string key, string defaultValue) {
                WeakReference<StoredValue> link;
                StoredValue result;


                if (!Instances.TryGetValue(key, out link) || !link.TryGetTarget(out result)) {
                    if (link != null) {
                        RemoveDeadReferences(Instances);
                    }

                    result = new StoredValue(key, defaultValue);
                    link = new WeakReference<StoredValue>(result);
                    Instances[key] = link;
                }

                return result;
            }

            private readonly string _key;
            private readonly string _defaultValue;

            private StoredValue(string key, string defaultValue) {
                _key = key;
                _defaultValue = defaultValue;
            }

            private string _value;

            public string Value {
                get { return _value ?? (_value = ValuesStorage.GetString(_key, _defaultValue)); }
                set {
                    if (Equals(value, Value)) return;
                    _value = value;
                    ValuesStorage.Set(_key, value);
                    OnPropertyChanged();
                }
            }
        }

        private void Initialize(string key, string defaultValue) {
            Source = StoredValue.Create(key, defaultValue);
            Path = new PropertyPath(nameof(StoredValue.Value));
            Mode = BindingMode.TwoWay;
        }
    }
}