using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Xaml.Schema;
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

        public static string GetValue(string key, object defaultValue = null) {
            return StoredValue.Create(key, defaultValue).Value;
        }

        public static void SetValue(string key, string value) {
            StoredValue.Create(key, null).Value = value;
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

            internal static StoredValue Create(string key, object defaultValue) {
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

            private readonly string _storageKey;
            private readonly string _defaultValue;

            private StoredValue(string key, object defaultValue) {
                _storageKey = @"_stored:" + key;
                _defaultValue = defaultValue?.ToString();
            }

            private string _value;

            public string Value {
                get => _value ?? (_value = ValuesStorage.GetString(_storageKey, _defaultValue));
                set {
                    if (Equals(value, Value)) return;
                    _value = value;
                    ValuesStorage.Set(_storageKey, value);
                    OnPropertyChanged();
                }
            }
        }

        private void Initialize(string key, object defaultValue) {
            Source = StoredValue.Create(key, defaultValue);
            Path = new PropertyPath(nameof(StoredValue.Value));
            Mode = BindingMode.TwoWay;
        }
    }
}