using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class ChangeableObservableCollection<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged where T : INotifyPropertyChanged {
        public ChangeableObservableCollection(){ }
        
        public ChangeableObservableCollection(IEnumerable<T> collection) {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            CopyFrom(collection);
        }

        private void CopyFrom(IEnumerable<T> collection) {
            var items = Items;
            if (collection == null || items == null) return;
            using (var enumerator = collection.GetEnumerator()) {
                while (enumerator.MoveNext()) {
                    var item = enumerator.Current;
                    Subscribe(item);
                    items.Add(item);
                }
            }
        }

        public event PropertyChangedEventHandler ItemPropertyChanged;

        protected virtual void Subscribe(T item) {
            item.PropertyChanged += Item_PropertyChanged;
        }

        protected virtual void Unsubscribe(T item) {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        protected void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            ItemPropertyChanged?.Invoke(sender, e);
        }

        public void Move(int oldIndex, int newIndex) {
            MoveItem(oldIndex, newIndex);
        }
        
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
            add { PropertyChanged += value; }
            remove { PropertyChanged -= value; }
        }

        public virtual event NotifyCollectionChangedEventHandler CollectionChanged;
        
        protected override void ClearItems() {
            CheckReentrancy();
            foreach (var item in Items) {
                Unsubscribe(item);
            }

            base.ClearItems();
            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionReset();
        }
        
        protected override void RemoveItem(int index) {
            CheckReentrancy();
            var removedItem = this[index];
            Unsubscribe(removedItem);

            base.RemoveItem(index);

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
        }

        protected override void InsertItem(int index, T item) {
            CheckReentrancy();
            Subscribe(item);
            base.InsertItem(index, item);

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        }
        protected override void SetItem(int index, T item) {
            CheckReentrancy();
            var originalItem = this[index];
            Unsubscribe(originalItem);
            Subscribe(item);
            base.SetItem(index, item);

            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Replace, originalItem, item, index);
        }

        /// <summary>
        /// Called by base class ObservableCollection&lt;T&gt; when an item is to be moved within the list;
        /// raises a CollectionChanged event to any listeners.
        /// </summary>
        protected void MoveItem(int oldIndex, int newIndex) {
            CheckReentrancy();
            var removedItem = this[oldIndex];

            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, removedItem);

            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Move, removedItem, newIndex, oldIndex);
        }
        
        protected void OnPropertyChanged(PropertyChangedEventArgs e) {
            PropertyChanged?.Invoke(this, e);
        }
        
        protected event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            var c = CollectionChanged;
            if (c == null) return;
            using (BlockReentrancy()) {
                c.Invoke(this, e);
            }
        }

        protected IDisposable BlockReentrancy() {
            _monitor.Enter();
            return _monitor;
        }
        
        protected void CheckReentrancy() {
            if (_monitor.Busy && CollectionChanged != null && CollectionChanged.GetInvocationList().Length > 1) {
                throw new InvalidOperationException();
            }
        }

        private void OnPropertyChanged(string propertyName) {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
        
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index) {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));
        }
        
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index, int oldIndex) {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index, oldIndex));
        }
        
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object oldItem, object newItem, int index) {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));
        }

        private void OnCollectionReset() {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private const string CountString = "Count";
        private const string IndexerName = "Item[]";

        private readonly SimpleMonitor _monitor = new SimpleMonitor();

        private class SimpleMonitor : IDisposable {
            public void Enter() {
                ++_busyCount;
            }

            public void Dispose() {
                --_busyCount;
            }

            public bool Busy => _busyCount > 0;

            private int _busyCount;
        }

        #region Additional methods
        public void AddRange(IEnumerable<T> range) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            var list = range.ToList();
            if (list.Count > 3 && list.Count > Count) {
                _AddRangeDirect(list);
            } else {
                foreach (var item in list) {
                    Add(item);
                }
            }
        }

        /// <summary>
        /// Don’t use it unless you have clear understanding how it differs from AddRange()!
        /// Will call Reset event afterwards, so don’t use it for small collections.
        /// </summary>
        /// <param name="range">Range.</param>
        public void _AddRangeDirect(IEnumerable<T> range) {
            foreach (var item in range) {
                Subscribe(item);
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void ReplaceEverythingBy([NotNull] IList<T> list) {
            foreach (var item in Items) {
                Unsubscribe(item);
            }

            Items.Clear();
            foreach (var item in list) {
                Subscribe(item);
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public virtual bool ReplaceIfDifferBy([NotNull] IEnumerable<T> range) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            var list = range as IList<T> ?? range.ToList();
            if (Items.SequenceEqual(list)) return false;

            ReplaceEverythingBy(list);
            return true;
        }

        public virtual void ReplaceEverythingBy([NotNull] IEnumerable<T> range) {
            if (range == null) throw new ArgumentNullException(nameof(range));

            // for cases when range is somehow created from Items
            ReplaceEverythingBy(range as IList<T> ?? range.ToList());
        }
        #endregion
    }
}