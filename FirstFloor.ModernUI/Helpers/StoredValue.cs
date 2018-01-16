using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class StoredValue : NotifyPropertyChanged {
        private static readonly Dictionary<string, WeakReference<StoredValue>> Instances = new Dictionary<string, WeakReference<StoredValue>>();

        private static void RemoveDeadReferences<TKey, TValue>([NotNull] IDictionary<TKey, WeakReference<TValue>> dictionary) where TValue : class {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            var toRemove = dictionary.Where(x => !x.Value.TryGetTarget(out _)).Select(x => x.Key).ToList();
            foreach (var key in toRemove) {
                dictionary.Remove(key);
            }
        }

        internal static StoredValue Create([NotNull] string key, [CanBeNull] object defaultValue) {
            if (!Instances.TryGetValue(key, out var link) || !link.TryGetTarget(out var result)) {
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

        protected StoredValue(string key, object defaultValue) {
            _storageKey = @"_stored:" + key;
            _defaultValue = defaultValue?.ToString();
        }

        private string _value;

        [CanBeNull]
        public string Value {
            get => _value ?? (_value = ValuesStorage.Get(_storageKey, _defaultValue));
            set {
                if (Equals(value, Value)) return;
                _value = value;
                ValuesStorage.Set(_storageKey, value);
                OnPropertyChanged();
            }
        }

        public StoredValue<T> GetStrict<T>(T defaultValue) {
            return new StoredValue<T>(this, defaultValue);
        }
    }

    public class StoredValue<T> : NotifyPropertyChanged {
        private readonly StoredValue _generic;
        private readonly T _defaultValue;

        internal StoredValue(StoredValue generic, T defaultValue) {
            _generic = generic;
            _defaultValue = defaultValue;
            Value = _generic.Value.As(_defaultValue);
            _generic.SubscribeWeak(OnPropertyChanged);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs.PropertyName == nameof(_generic.Value)) {
                Value = _generic.Value.As(_defaultValue);
            }
        }

        private T _value;

        public T Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }
    }
}