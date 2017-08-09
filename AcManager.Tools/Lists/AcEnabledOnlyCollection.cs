using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.Lists {
    public class AcEnabledOnlyCollection<T> : AcEnabledOnlyCollection_ThirdImpl<T> where T : AcObjectNew {
        public AcEnabledOnlyCollection(IAcWrapperObservableCollection collection) : base(collection) { }
    }

    // Third implementation, now using proper WrappedFilteredCollection thing.
    public class AcEnabledOnlyCollection_ThirdImpl<T> : WrappedFilteredCollection<AcItemWrapper, T>
            where T : AcObjectNew {
        public AcEnabledOnlyCollection_ThirdImpl([NotNull] IAcWrapperObservableCollection collection) : base((IReadOnlyList<AcItemWrapper>)collection) {
            collection.WrappedValueChanged += OnWrappedValueChanged;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            Refresh((AcItemWrapper)sender);
        }

        protected override T Wrap(AcItemWrapper source) {
            return (T)source.Value;
        }

        protected override bool Test(AcItemWrapper source) {
            return source.IsLoaded && source.Value.Enabled;
        }
    }

    // Second implementation — uses WrappedCollection to convert AcItemWrapper to AcObjectNew, and then ListCollectionView
    // to get rid of NULLs. In theory (if both WrappedCollection and ListCollectionView do not contain errors, which is,
    // in all honesty, is unlikely) should be very reliable, but slower.
    public class AcEnabledOnlyCollection_WrappedImpl<T> : IList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged, IWeakEventListener
            where T : AcObjectNew {
        private readonly DelegateWrappedCollection<AcItemWrapper, T> _wrapped;
        private readonly BetterListCollectionView _view;

        public AcEnabledOnlyCollection_WrappedImpl(IAcWrapperObservableCollection collection) {
            _wrapped = new DelegateWrappedCollection<AcItemWrapper, T>((IReadOnlyList<AcItemWrapper>)collection, x => x.Value.Enabled ? x.Value as T : null);
            _view = new BetterListCollectionView(_wrapped) { Filter = o => o != null };
            collection.WrappedValueChanged += OnWrappedValueChanged;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            _wrapped.Refresh((AcItemWrapper)sender);
        }

        public IEnumerator<T> GetEnumerator() {
            return _wrapped.NonNull().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            throw new NotSupportedException();
        }

        public int Add(object value) {
            throw new NotSupportedException();
        }

        public void Clear() {
            throw new NotSupportedException();
        }

        public void Insert(int index, object value) {
            throw new NotSupportedException();
        }

        public void Remove(object value) {
            throw new NotSupportedException();
        }

        public bool Remove(T item) {
            throw new NotSupportedException();
        }

        public bool Contains(T item) {
            return item != null && _wrapped.Contains(item);
        }

        public bool Contains(object value) {
            return value != null && _wrapped.Contains(value);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            foreach (var v in _wrapped.NonNull()) {
                array[arrayIndex++] = v;
            }
        }

        public void CopyTo(Array array, int index) {
            foreach (var v in _wrapped.NonNull()) {
                array.SetValue(v, index++);
            }
        }

        public int Count => _view.Count;
        public object SyncRoot => _wrapped.SyncRoot;
        public bool IsSynchronized => _wrapped.IsSynchronized;
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;

        public event NotifyCollectionChangedEventHandler CollectionChanged {
            add => ((INotifyCollectionChanged)_view).CollectionChanged += value;
            remove => ((INotifyCollectionChanged)_view).CollectionChanged -= value;
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add => ((INotifyPropertyChanged)_view).PropertyChanged += value;
            remove => ((INotifyPropertyChanged)_view).PropertyChanged -= value;
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            return _view.ReceiveWeakEvent(managerType, sender, e);
        }

        public int IndexOf(T item) {
            return _view.IndexOf(item);
        }

        public int IndexOf(object value) {
            return _view.IndexOf(value);
        }

        public void Insert(int index, T item) {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index) {
            throw new NotSupportedException();
        }

        public T this[int index] {
            get => _wrapped.NonNull().ElementAt(index);
            set => throw new NotSupportedException();
        }

        object IList.this[int index] {
            get => _wrapped.NonNull().ElementAt(index);
            set => throw new NotSupportedException();
        }
    }

    // First implementation, do not keep order and, it appears, sometimes loses elements. ¯\_(ツ)_/¯
    public class AcEnabledOnlyCollection_NaiveImpl<T> : BetterObservableCollection<T>, IWeakEventListener where T : AcObjectNew {
        private readonly IAcWrapperObservableCollection _collection;

        public AcEnabledOnlyCollection_NaiveImpl(IAcWrapperObservableCollection collection)
                : base(collection.Select(x => x.Value).Where(x => x.Enabled).OfType<T>()) {
            _collection = collection;
            collection.CollectionChanged += OnCollectionChanged;
            collection.WrappedValueChanged += OnWrappedValueChanged;
        }

        private void Rebuild() {
            ReplaceEverythingBy(_collection.Select(x => x.Value).Where(x => x.Enabled).OfType<T>());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    AddRange(e.NewItems.Cast<AcItemWrapper>().Select(x => x.Value).Where(x => x.Enabled).OfType<T>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>()) {
                        Remove(item);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace: {
                    foreach (var item in e.OldItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>()) {
                        Remove(item);
                    }

                    AddRange(e.NewItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>().Where(x => x.Enabled));
                    break;
                }

                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Reset:
                    Rebuild();
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            var o = e.OldValue as T;
            if (o != null) {
                Remove(o);
            }

            var n = e.NewValue as T;
            if (n?.Enabled != true) return;

            var i = _collection.FindIndex(x => x.Value == n);
            if (i == -1 || i == _collection.Count - 1) {
                Add(n);
                return;
            }

            var after = _collection.Take(i).LastOrDefault(x => x.IsLoaded && x.Value.Enabled);
            if (after == null) {
                Insert(0, n);
                return;
            }

            var afterLocal = Items.IndexOf(after.Value);
            if (afterLocal == -1 || afterLocal == Items.Count - 1) {
                Add(n);
                return;
            }

            Insert(afterLocal + 1, n);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            var notify = e as NotifyCollectionChangedEventArgs;
            if (notify != null) {
                OnCollectionChanged(sender, notify);
                return true;
            }

            var wrapped = e as WrappedValueChangedEventArgs;
            if (wrapped != null) {
                OnWrappedValueChanged(sender, wrapped);
                return true;
            }

            return false;
        }
    }
}
