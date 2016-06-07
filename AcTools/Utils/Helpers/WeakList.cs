using System;
using System.Linq;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public class WeakList<T> : IList<T> where T : class {
        private readonly List<WeakReference<T>> _innerList = new List<WeakReference<T>>();

        public IEnumerable<T> NonNull() {
            T r;
            return _innerList.Select(reference => reference.TryGetTarget(out r) ? r : null);
        }

        #region IList<T> Members
        public int IndexOf(T item) {
            return NonNull().IndexOf(item);
        }

        public void Insert(int index, T item) {
            _innerList.Insert(index, new WeakReference<T>(item));
        }

        public void RemoveAt(int index) {
            _innerList.RemoveAt(index);
        }

        [CanBeNull]
        public T this[int index] {
            get {
                T r;
                return _innerList[index].TryGetTarget(out r) ? r : null;
            }
            set { _innerList[index] = new WeakReference<T>(value); }
        }
        #endregion

        #region ICollection<T> Members
        public void Add(T item) {
            _innerList.Add(new WeakReference<T>(item));
        }

        public void Clear() {
            _innerList.Clear();
        }

        public bool Contains(T item) {
            return NonNull().Any(x => Equals(x, item));
        }

        public void CopyTo(T[] array, int arrayIndex) {
            foreach (var v in NonNull()) {
                array[arrayIndex++] = v;
            }
        }

        public int Count => _innerList.Count;

        public bool IsReadOnly => false;

        public bool Remove(T item) {
            var index = IndexOf(item);
            if (index <= -1) return false;
            RemoveAt(index);
            return true;
        }
        #endregion

        #region IEnumerable<T> Members
        public IEnumerator<T> GetEnumerator() {
            return NonNull().GetEnumerator();
        }
        #endregion

        #region IEnumerable Members
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        #endregion

        public void Purge() {
            T r;
            _innerList.RemoveAll(x => !x.TryGetTarget(out r));
        }
    }
}