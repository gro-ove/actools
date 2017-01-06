using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class BetterObservableCollection<T> : ObservableCollection<T> {
        public BetterObservableCollection() {}

        public BetterObservableCollection(List<T> list) : base(list) {}

        public BetterObservableCollection([NotNull] IEnumerable<T> collection) : base(collection) {}

        public void AddRange(IEnumerable<T> range) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            var list = range.ToList();
            if (list.Count > 3 && list.Count > Count) {
                foreach (var item in list) {
                    Items.Add(item);
                }

                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            } else {
                foreach (var item in list) {
                    Add(item);
                }
            }
        }

        private void ReplaceEverythingByInner([NotNull] IEnumerable<T> list) {
            Items.Clear();
            foreach (var item in list) {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool ReplaceIfDifferBy([NotNull] IEnumerable<T> range, IEqualityComparer<T> comparer) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            var list = range as IList<T> ?? range.ToList();
            var items = Items;
            if (items.Count == list.Count) {
                var c = list.Count;
                for (var i = 0; i < c; i++) {
                    if (!comparer.Equals(list[i], items[i])) {
                        goto Replace;
                    }
                }

                return false;
            }

            Replace:
            ReplaceEverythingByInner(list);
            return true;
        }

        public bool ReplaceIfDifferBy([NotNull] IEnumerable<T> range) {
            return ReplaceIfDifferBy(range, EqualityComparer<T>.Default);
        }

        public void ReplaceEverythingBy([NotNull] IEnumerable<T> range) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            // for cases when range is somehow created from Items
            ReplaceEverythingByInner(range as IList<T> ?? range.ToList());
        }

        /// <summary>
        /// Please, use it only if you’re absolutely sure IEnumerable in no way
        /// origins from this collection.
        /// </summary>
        public void ReplaceEverythingBy_Direct([NotNull] IEnumerable<T> range) {
            ReplaceEverythingByInner(range);
        }
    }
}
