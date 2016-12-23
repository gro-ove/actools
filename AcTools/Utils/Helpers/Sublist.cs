using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public class Sublist {
        public static Sublist<T> Create<T>([NotNull] IList list, int offset, int count) {
            return new Sublist<T>(list, offset, count);
        }

        public static Sublist<T> Create<T>([NotNull] IList<T> list, int offset, int count) {
            return new Sublist<T>(list, offset, count);
        }
    }

    public class Sublist<T> : IList<T>, IList, IReadOnlyList<T> {
        [NotNull]
        private readonly IList _list;

        [CanBeNull]
        private readonly IList<T> _typed;

        private readonly int _offset;
        private int _count;

        public Sublist([NotNull] IList list, int offset, int count) {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (offset < 0 || offset >= list.Count) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count >= list.Count) throw new ArgumentOutOfRangeException(nameof(count));

            _list = list;
            _typed = list as IList<T>;
            _offset = offset;
            _count = count;
        }

        public Sublist([NotNull] IList<T> list, int offset, int count) {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (offset < 0 || offset >= list.Count) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count >= list.Count) throw new ArgumentOutOfRangeException(nameof(count));

            _list = (IList)list;
            _typed = list;
            _offset = offset;
            _count = count;
        }

        // TODO: Optimize
        public IEnumerator<T> GetEnumerator() {
            return (_typed ?? _list.OfType<T>()).Skip(_offset).Take(_count).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            _list.Insert(_offset + _count, item);
            _count++;
        }

        int IList.Add(object value) {
            _list.Insert(_offset + _count, value);
            return _count++;
        }

        bool IList.Contains(object value) {
            for (int i = _offset, c = _offset + _count; i < c; i++) {
                if (Equals(_list[i], value)) return true;
            }
            return false;
        }

        public int IndexOf(object value) {
            for (var i = 0; i < _count; i++) {
                if (Equals(_list[i + _offset], value)) {
                    return i;
                }
            }
            return -1;
        }

        void IList.Insert(int index, object value) {
            if (index < 0 || index > _count) throw new ArgumentOutOfRangeException(nameof(index));
            _list.Insert(_offset + index, value);
            _count++;
        }

        private bool RemoveInternal(object value) {
            for (int i = _offset, c = _offset + _count; i < c; i++) {
                if (Equals(_list[i], value)) {
                    _list.RemoveAt(i);
                    _count--;
                    return true;
                }
            }

            return false;
        }

        void IList.Remove(object value) {
            RemoveInternal(value);
        }

        object IList.this[int index] {
            get {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                return _list[_offset + index];
            }
            set {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                _list[_offset + index] = value;
            }
        }

        public bool IsReadOnly => _list.IsReadOnly;

        public bool IsFixedSize => _list.IsFixedSize;

        public void Clear() {
            for (var i = _offset + _count - 1; i >= _offset; i--) {
                _list.RemoveAt(i);
            }
            _count = 0;
        }

        public bool Contains(T item) {
            return ((IList)this).Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            if (_typed != null) {
                for (int i = 0, j = arrayIndex; i < _count && j < array.Length; i++, j++) {
                    array[j] = _typed[i + _offset];
                }
            } else {
                for (int i = 0, j = arrayIndex; i < _count && j < array.Length; i++, j++) {
                    array[j] = (T)_list[i + _offset];
                }
            }
        }

        public bool Remove(T item) {
            return RemoveInternal(item);
        }

        public void CopyTo(Array array, int index) {
            for (int i = 0, j = index; i < _count && j < array.Length; i++, j++) {
                array.SetValue(_list[i + _offset], j);
            }
        }

        public int Count => _count;

        public object SyncRoot => _list.SyncRoot;

        public bool IsSynchronized => _list.IsSynchronized;

        public int IndexOf(T item) {
            return ((IList)this).IndexOf(item);
        }

        public void Insert(int index, T item) {
            ((IList)this).Insert(index, item);
        }

        public void RemoveAt(int index) {
            if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
            _list.RemoveAt(_offset + index);
            _count--;
        }

        public T this[int index] {
            get {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                return _typed != null ? _typed[_offset + index] : (T)_list[_offset + index];
            }
            set {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                _list[_offset + index] = value;
            }
        }
    }
}