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
            Initialize(@"_stored");
        }

        public Stored(string path) : base(path) {
            Initialize(@"_stored:" + path);
        }

        private class SourceInner : NotifyPropertyChanged {
            private static readonly Dictionary<string, WeakReference<SourceInner>> Instances = new Dictionary<string, WeakReference<SourceInner>>();

            private static void RemoveDeadReferences<TKey, TValue>([NotNull] IDictionary<TKey, WeakReference<TValue>> dictionary) where TValue : class {
                if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

                TValue temp;
                var toRemove = dictionary.Where(x => !x.Value.TryGetTarget(out temp)).Select(x => x.Key).ToList();

                foreach (var key in toRemove) {
                    dictionary.Remove(key);
                }
            }

            public static SourceInner Create(string key) {
                WeakReference<SourceInner> link;
                SourceInner result;


                if (!Instances.TryGetValue(key, out link) || !link.TryGetTarget(out result)) {
                    if (link != null) {
                        RemoveDeadReferences(Instances);
                    }

                    result = new SourceInner(key);
                    link = new WeakReference<SourceInner>(result);
                    Instances[key] = link;
                }

                return result;
            }

            private readonly string _key;

            private SourceInner(string key) {
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
            Source = SourceInner.Create(key);
            Path = new PropertyPath(nameof(SourceInner.Value));
            Mode = BindingMode.TwoWay;
        }
    }
}