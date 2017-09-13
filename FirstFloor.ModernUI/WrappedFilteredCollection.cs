using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class WrappedFilteredCollection {
        public static WrappedFilteredCollection<TSource, TWrapper> Create<TSource, TWrapper>(IReadOnlyList<TSource> collection,
                Func<TSource, TWrapper> wrap, Func<TSource, bool> test) {
            return new DelegateWrappedFilteredCollection<TSource, TWrapper>(collection, wrap, test);
        }
    }

    public class DelegateWrappedFilteredCollection<TSource, TWrapper> : WrappedFilteredCollection<TSource, TWrapper> {
        private readonly Func<TSource, TWrapper> _wrap;
        private readonly Func<TSource, bool> _test;

        public DelegateWrappedFilteredCollection(IReadOnlyList<TSource> collection, Func<TSource, TWrapper> wrap, Func<TSource, bool> test) : base(collection) {
            _wrap = wrap;
            _test = test;
        }

        protected override TWrapper Wrap(TSource source) {
            return _wrap(source);
        }

        protected override bool Test(TSource source) {
            return _test(source);
        }
    }

    public abstract class WrappedFilteredCollection<TSource, TWrapper> : WrappedFilteredCollection, IList<TWrapper>, IList, IReadOnlyList<TWrapper>, INotifyCollectionChanged,
            INotifyPropertyChanged {
        [NotNull]
        private readonly IReadOnlyList<TSource> _source;

        [CanBeNull]
        private List<TWrapper> _wrapped;

        [CanBeNull]
        private List<int> _indexes;

        protected WrappedFilteredCollection([NotNull] IReadOnlyList<TSource> collection) {
            _source = collection ?? throw new ArgumentNullException(nameof(collection));

            var notify = collection as INotifyCollectionChanged;
            if (notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(notify, nameof(notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }
        }

        private void EnsureRebuilt(out List<TWrapper> wrapped, out List<int> indexes) {
            if (_wrapped != null && _indexes != null) {
                wrapped = _wrapped;
                indexes = _indexes;
                return;
            }

            wrapped = new List<TWrapper>();
            indexes = new List<int>();

            for (var i = 0; i < _source.Count; i++) {
                var source = _source[i];
                if (Test(source)) {
                    wrapped.Add(Wrap(source));
                    indexes.Add(i);
                }
            }

            _wrapped = wrapped;
            _indexes = indexes;
        }

        private void Reset() {
            Debug.WriteLine(@"WrappedFilteredCollection.Reset()");
            _wrapped = null;
            _indexes = null;
            OnCountAndIndexerChanged();
            OnCollectionReset();
        }

        private int SourceIndexOf(TSource value) {
            for (var i = 0; i < _source.Count; i++) {
                if (Equals(_source[i], value)) return i;
            }

            return -1;
        }

        private static int SourceIndexToWrapped(List<int> indexes, int sourceIndex) {
            for (var i = 0; i < indexes.Count; i++) {
                var v = indexes[i];
                if (v == sourceIndex) return i;
                if (v > sourceIndex) return -1;
            }

            return -1;
        }

        private int SourceIndexToWrapped(int sourceIndex) {
            EnsureRebuilt(out var _, out var indexes);
            return SourceIndexToWrapped(indexes, sourceIndex);
        }

        private int FindWhereToInsert(int sourceIndex) {
            EnsureRebuilt(out var _, out var indexes);
            while (sourceIndex >= 0) {
                var v = SourceIndexToWrapped(indexes, sourceIndex--);
                if (v != -1) return v + 1;
            }

            return 0;
        }

        public void Refresh(TSource source) {
            var sourceIndex = SourceIndexOf(source);
            if (sourceIndex == -1) return;

            var wrapped = _wrapped;
            var indexes = _indexes;
            if (wrapped == null || indexes == null) {
                Reset();
                return;
            }

            var wrappedIndex = SourceIndexToWrapped(sourceIndex);
            if (!Test(source)) {
                if (wrappedIndex == -1) {
                    // Item was filtered out before and is filtered out now
                    return;
                }

                // Item is filtered out now
                var oldItem = wrapped[wrappedIndex];
                wrapped.RemoveAt(wrappedIndex);
                indexes.RemoveAt(wrappedIndex);
                OnIndexerChanged();
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItem, wrappedIndex);
            } else {
                var newItem = Wrap(source);
                if (wrappedIndex == -1) {
                    // Before, item was filtered out, but not anymore
                    var destination = FindWhereToInsert(sourceIndex - 1);
                    wrapped.Insert(destination, newItem);
                    indexes.Insert(destination, sourceIndex);
                    OnIndexerChanged();
                    OnCollectionChanged(NotifyCollectionChangedAction.Add, newItem, destination);
                    return;
                }

                // Item was preset and is present now
                var oldItem = wrapped[wrappedIndex];
                if (!Equals(oldItem, newItem)) {
                    wrapped[wrappedIndex] = newItem;
                    OnIndexerChanged();
                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, newItem, wrappedIndex);
                }
            }
        }

        private void TestAndAddNewItem(List<TWrapper> wrapped,  List<int> indexes, object item, int index) {
            var addedSource = (TSource)item;
            if (!Test(addedSource)) return;

            var newItem = Wrap(addedSource);
            var destination = FindWhereToInsert(index);
            wrapped.Insert(destination, newItem);
            indexes.Insert(destination, index);
            OnCountAndIndexerChanged();
            OnCollectionChanged(NotifyCollectionChangedAction.Add, newItem, destination);
        }

        private void RemoveExistingItem(List<TWrapper> wrapped,  List<int> indexes, int index) {
            var wrappedIndex = SourceIndexToWrapped(index);
            if (wrappedIndex == -1) return;

            var item = wrapped[wrappedIndex];
            wrapped.RemoveAt(wrappedIndex);
            indexes.RemoveAt(wrappedIndex);
            OnCountAndIndexerChanged();
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, wrappedIndex);
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            var wrapped = _wrapped;
            var indexes = _indexes;
            if (wrapped == null || indexes == null) {
                Reset();
                return;
            }

            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems != null && e.OldItems.Count > 0) {
                        Reset();
                    } else {
                        TestAndAddNewItem(wrapped, indexes, e.NewItems[0], e.NewStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null || e.OldItems.Count != 1 || e.NewItems != null && e.NewItems.Count > 0 ||
                            e.OldStartingIndex >= wrapped.Count) {
                        Reset();
                    } else {
                        RemoveExistingItem(wrapped, indexes, e.OldStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null && e.OldItems.Count != 1 || e.NewItems == null || e.NewItems.Count != 1 ||
                            e.OldStartingIndex >= wrapped.Count) {
                        Reset();
                    } else {
                        var wrappedIndex = SourceIndexToWrapped(e.OldStartingIndex);
                        if (wrappedIndex == -1) {
                            TestAndAddNewItem(wrapped, indexes, e.NewItems[0], e.OldStartingIndex);
                        } else {
                            var replacement = (TSource)e.NewItems[0];
                            if (Test(replacement)) {
                                var oldItem = wrapped[wrappedIndex];
                                var newItem = Wrap(replacement);
                                if (!Equals(oldItem, newItem)) {
                                    wrapped[wrappedIndex] = newItem;
                                    OnIndexerChanged();
                                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, newItem, wrappedIndex);
                                }
                            } else {
                                RemoveExistingItem(wrapped, indexes, e.OldStartingIndex);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems != null && e.OldItems.Count != 1 || e.NewItems != null && e.NewItems.Count != 1 ||
                            e.OldStartingIndex >= wrapped.Count) {
                        Reset();
                    } else {
                        var wrappedIndex = SourceIndexToWrapped(e.OldStartingIndex);
                        if (wrappedIndex != -1) {
                            var item = wrapped[wrappedIndex];
                            wrapped.RemoveAt(wrappedIndex);
                            indexes.RemoveAt(wrappedIndex);

                            var destination = FindWhereToInsert(e.NewStartingIndex);
                            wrapped.Insert(destination, item);
                            indexes.Insert(destination, e.NewStartingIndex);

                            OnIndexerChanged();
                            OnCollectionChanged(NotifyCollectionChangedAction.Move, item, destination, wrappedIndex);
                        }
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
        protected abstract bool Test(TSource source);

        #region IList, IReadOnlyList methods
        public IEnumerator<TWrapper> GetEnumerator() {
            EnsureRebuilt(out var wrapped, out var _);
            return wrapped.GetEnumerator();
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
            EnsureRebuilt(out var wrapped, out var _);
            return wrapped.Contains(item);
        }

        public void CopyTo(TWrapper[] array, int arrayIndex) {
            EnsureRebuilt(out var wrapped, out var _);
            wrapped.CopyTo(array, arrayIndex);
        }

        public bool Remove(TWrapper item) {
            throw new NotSupportedException();
        }

        public void CopyTo(Array array, int index) {
            EnsureRebuilt(out var wrapped, out var _);
            var wrappers = array as TWrapper[];
            if (wrappers != null) {
                wrapped.CopyTo(wrappers, index);
            } else {
                var objects = (object[])array;
                var count = wrapped.Count;
                for (var i = 0; i < count; i++) {
                    objects[index++] = wrapped[i];
                }
            }
        }

        public int Count{
            get {
                EnsureRebuilt(out var wrapped, out var _);
                return wrapped.Count;
            }}

        [NonSerialized]
        private object _syncRoot;

        public object SyncRoot {
            get {
                if (_syncRoot == null) {
                    EnsureRebuilt(out var wrapped, out var _);
                    if (wrapped != null) {
                        _syncRoot = ((ICollection)wrapped).SyncRoot;
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
            EnsureRebuilt(out var wrapped, out var _);
            return wrapped.IndexOf(item);
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
                EnsureRebuilt(out var wrapped, out var _);
                return wrapped[index];
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