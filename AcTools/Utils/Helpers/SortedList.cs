using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AcTools.Utils.Helpers {
    /// <summary>
    /// Always sorted list. Please, keep in mind that setting items by index and using 
    /// Insert don’t work as usual here.
    /// </summary>
    /// <typeparam name="T">Anything.</typeparam>
    public class SortedList<T> : IList<T>, IList, IReadOnlyList<T> {
        private readonly List<T> _list;

        private readonly IComparer<T> _comparer;

        public SortedList(IComparer<T> comparer = null) {
            _comparer = comparer ?? Comparer<T>.Default;
            _list = new List<T>();
        }

        public SortedList(int capacity, IComparer<T> comparer = null) {
            _comparer = comparer ?? Comparer<T>.Default;
            _list = new List<T>(capacity);
        }

        public SortedList(IEnumerable<T> collection, IComparer<T> comparer = null) {
            _comparer = comparer ?? Comparer<T>.Default;
            _list = new List<T>(collection.OrderBy(x => x, comparer));
        }

        public int Capacity {
            get { return _list.Capacity; }
            set { _list.Capacity = value; }
        }

        public IEnumerator<T> GetEnumerator() {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private int AddInner(T value) {
            var end = _list.Count - 1;

            // Array is empty or new item should go in the end
            if (end == -1 || _comparer.Compare(value, _list[end]) > 0) {
                _list.Add(value);
                return end + 1;
            }

            // Simplest version for small arrays
            if (end < 20) {
                for (end--; end >= 0; end--) {
                    if (_comparer.Compare(value, _list[end]) >= 0) {
                        _list.Insert(end + 1, value);
                        return end + 1;
                    }
                }

                _list.Insert(0, value);
                return _list.Count - 1;
            }

            // Sort of binary search
            var start = 0;
            while (true) {
                if (end == start) {
                    _list.Insert(start, value);
                    return start;
                }

                if (end == start + 1) {
                    if (_comparer.Compare(value, _list[start]) <= 0) {
                        _list.Insert(start, value);
                        return start;
                    }

                    _list.Insert(end, value);
                    return end;
                }

                var m = start + (end - start) / 2;

                var c = _comparer.Compare(value, _list[m]);
                if (c == 0) {
                    _list.Insert(m, value);
                    return m;
                }

                if (c < 0) {
                    end = m;
                } else {
                    start = m + 1;
                }
            }
        }

        public void Add(T item) {
            AddInner(item);
        }

        int IList.Add(object value) {
            return AddInner((T)value);
        }

        bool IList.Contains(object value) {
            return ((IList)_list).Contains(value);
        }

        public void Clear() {
            _list.Clear();
        }

        int IList.IndexOf(object value) {
            return ((IList)_list).IndexOf(value);
        }

        void IList.Insert(int index, object value) {
            Add((T)value);
        }

        void IList.Remove(object value) {
            ((IList)_list).Remove(value);
        }

        void IList.RemoveAt(int index) {
            _list.RemoveAt(index);
        }

        object IList.this[int index] {
            get { return _list[index]; }
            set {
                _list.RemoveAt(index);
                Add((T)value);
            }
        }

        bool IList.IsReadOnly => false;

        bool IList.IsFixedSize { get; } = false;

        void ICollection<T>.Clear() {
            _list.Clear();
        }

        // TODO: optimize using binary search, but keep in mind that if _comparer says items
        // are equal, they still might be different
        public bool Contains(T item) {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item) {
            var index = IndexOf(item);
            if (index >= 0) {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        void ICollection.CopyTo(Array array, int index) {
            ((ICollection)_list).CopyTo(array, index);
        }

        int ICollection.Count => _list.Count;

        object ICollection.SyncRoot => ((ICollection)_list).SyncRoot;

        bool ICollection.IsSynchronized => ((ICollection)_list).IsSynchronized;

        int ICollection<T>.Count => _list.Count;

        bool ICollection<T>.IsReadOnly => false;

        // TODO: optimize using binary search, but keep in mind that if _comparer says items
        // are equal, they still might be different
        public int IndexOf(T item) {
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item) {
            Add(item);
        }

        public void RemoveAt(int index) {
            _list.RemoveAt(index);
        }

        public T this[int index] {
            get { return _list[index]; }
            set {
                _list.RemoveAt(index);
                Add(value);
            }
        }

        public int Count => _list.Count;

        T IReadOnlyList<T>.this[int index] => _list[index];
    }
}