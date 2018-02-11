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

        public void Sort(IComparer<T> comparer) {
            if (Items is List<T> itemsList) {
                itemsList.Sort(comparer);
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            } else {
                ReplaceEverythingBy(Items.OrderBy(x => x, comparer));
            }
        }

        private void ReplaceEverythingByInner([NotNull] IEnumerable<T> list) {
            var items = Items;
            items.Clear();
            if (items is List<T> itemsList) {
                itemsList.AddRange(list);
            } else {
                foreach (var item in list) {
                    items.Add(item);
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void ReplaceEverythingByInner([NotNull] IList<T> list) {
            var items = Items;
            items.Clear();
            if (items is List<T> itemsList) {
                itemsList.AddRange(list);
            } else {
                foreach (var item in list) {
                    items.Add(item);
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool ReplaceIfDifferBy([NotNull] IEnumerable<T> range, IEqualityComparer<T> comparer) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            var list = range as IList<T> ?? range.ToList();
            var items = Items;

            if (items.Count != list.Count) {
                ReplaceEverythingByInner(list);
                return true;
            }

            for (var i = list.Count - 1; i >= 0; i--) {
                if (!comparer.Equals(list[i], items[i])) {
                    ReplaceEverythingByInner(list);
                    return true;
                }
            }

            return false;
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
