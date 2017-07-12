using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class WrappedCollection {
        public static WrappedCollection<TSource, TWrapper> Create<TSource, TWrapper>(IReadOnlyList<TSource> collection,
                Func<TSource, TWrapper> wrap) {
            return new DelegateWrappedCollection<TSource, TWrapper>(collection, wrap);
        }
    }

    public class DelegateWrappedCollection<TSource, TWrapper> : WrappedCollection<TSource, TWrapper> {
        private readonly Func<TSource, TWrapper> _wrap;

        public DelegateWrappedCollection(IReadOnlyList<TSource> collection, Func<TSource, TWrapper> wrap) : base(collection) {
            _wrap = wrap;
        }

        protected override TWrapper Wrap(TSource source) {
            return _wrap(source);
        }
    }

    public abstract class WrappedCollection<TSource, TWrapper> : WrappedCollection, IList<TWrapper>, IList, IReadOnlyList<TWrapper>, INotifyCollectionChanged,
            INotifyPropertyChanged {
        [NotNull]
        private readonly IReadOnlyList<TSource> _source;

        [CanBeNull]
        private List<TWrapper> _items;

        protected WrappedCollection([NotNull] IReadOnlyList<TSource> collection) {
            _source = collection ?? throw new ArgumentNullException(nameof(collection));

            var notify = collection as INotifyCollectionChanged;
            if (notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(notify, nameof(notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }
        }

        [NotNull]
        private List<TWrapper> Rebuild() {
            return _source.Select(Wrap).ToList();
        }

        private void Reset() {
            Debug.WriteLine(@"WrappedCollection.Reset()");
            _items = null;
            OnCountAndIndexerChanged();
            OnCollectionReset();
        }

        private int IndexOf(TSource value) {
            for (var i = 0; i < _source.Count; i++) {
                if (Equals(_source[i], value)) return i;
            }

            return -1;
        }

        public void Refresh(TSource source) {
            var index = IndexOf(source);
            if (index == -1) return;

            var items = _items;
            if (items == null) {
                Reset();
                return;
            }

            var oldItem = items[index];
            var newItem = Wrap(source);
            if (!Equals(oldItem, newItem)) {
                items[index] = newItem;
                OnIndexerChanged();
                OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, newItem, index);
            }
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            var items = _items;
            if (items == null) {
                Reset();
                return;
            }

            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems != null && e.OldItems.Count > 0) {
                        Reset();
                    } else {
                        var item = Wrap((TSource)e.NewItems[0]);
                        items.Insert(e.NewStartingIndex, item);
                        OnCountAndIndexerChanged();
                        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, e.NewStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null || e.OldItems.Count != 1 || e.NewItems != null && e.NewItems.Count > 0 ||
                            e.OldStartingIndex >= items.Count) {
                        Reset();
                    } else {
                        var item = items[e.OldStartingIndex];
                        items.RemoveAt(e.OldStartingIndex);
                        OnCountAndIndexerChanged();
                        OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null && e.OldItems.Count != 1 || e.NewItems == null || e.NewItems.Count != 1 ||
                            e.OldStartingIndex >= items.Count) {
                        Reset();
                    } else {
                        var oldItem = items[e.OldStartingIndex];
                        var newItem = Wrap((TSource)e.NewItems[0]);
                        items[e.OldStartingIndex] = newItem;
                        OnIndexerChanged();
                        OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, newItem, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems != null && e.OldItems.Count != 1 || e.NewItems != null && e.NewItems.Count != 1 ||
                            e.OldStartingIndex >= items.Count) {
                        Reset();
                    } else {
                        var item = items[e.OldStartingIndex];
                        items.RemoveAt(e.OldStartingIndex);
                        items.Insert(e.NewStartingIndex, item);

                        OnIndexerChanged();
                        OnCollectionChanged(NotifyCollectionChangedAction.Move, item, e.NewStartingIndex, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Reset();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected abstract TWrapper Wrap(TSource source);

        #region IList, IReadOnlyList methods
        public IEnumerator<TWrapper> GetEnumerator() {
            if (_items == null) _items = Rebuild();
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(TWrapper item) {
            throw new NotSupportedException();
        }

        int IList.Add(object value) {
            throw new NotSupportedException();
        }

        private static bool IsCompatibleObject(object value) {
            return value is TWrapper || value == null && default(TWrapper) == null;
        }

        bool IList.Contains(object value) {
            return IsCompatibleObject(value) && Contains((TWrapper)value);
        }

        public void Clear() {
            throw new NotSupportedException();
        }

        int IList.IndexOf(object value) {
            return IsCompatibleObject(value) ? IndexOf((TWrapper)value) : -1;
        }

        void IList.Insert(int index, object value) {
            throw new NotSupportedException();
        }

        void IList.Remove(object value) {
            throw new NotSupportedException();
        }

        public bool Contains(TWrapper item) {
            if (_items == null) _items = Rebuild();
            return _items.Contains(item);
        }

        public void CopyTo(TWrapper[] array, int arrayIndex) {
            if (_items == null) _items = Rebuild();
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(TWrapper item) {
            throw new NotSupportedException();
        }

        public void CopyTo(Array array, int index) {
            if (_items == null) _items = Rebuild();

            var wrappers = array as TWrapper[];
            if (wrappers != null) {
                _items.CopyTo(wrappers, index);
            } else {
                var objects = (object[])array;
                var count = _items.Count;
                for (int i = 0; i < count; i++) {
                    objects[index++] = _items[i];
                }
            }
        }

        public int Count => _source.Count;

        [NonSerialized]
        private object _syncRoot;

        public object SyncRoot {
            get {
                if (_syncRoot == null) {
                    ICollection c = _items;
                    if (c != null) {
                        _syncRoot = c.SyncRoot;
                    } else {
                        System.Threading.Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);
                    }
                }
                return _syncRoot;
            }
        }

        public bool IsSynchronized => false;

        public bool IsReadOnly => true;

        public bool IsFixedSize => false;

        public int IndexOf(TWrapper item) {
            if (_items == null) _items = Rebuild();
            return _items.IndexOf(item);
        }

        public void Insert(int index, TWrapper item) {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index) {
            throw new NotSupportedException();
        }

        object IList.this[int index] {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public TWrapper this[int index] {
            get {
                if (_items == null) _items = Rebuild();
                return _items[index];
            }
            set => throw new NotSupportedException();
        }
        #endregion

        #region INotifyCollectionChanged, INotifyPropertyChanged events
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region On-Methods
        private void OnIndexerChanged() {
            OnPropertyChanged(@"Item[]");
        }

        private void OnCountAndIndexerChanged() {
            OnPropertyChanged(nameof(Count));
            OnIndexerChanged();
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e) {
            PropertyChanged?.Invoke(this, e);
        }

        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            CollectionChanged?.Invoke(this, e);
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
        #endregion Private Methods
    }
}