using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.Tools {
    public static class Holder {
        public static Holder<T> CreateNonHolding<T>(T value) {
            return new Holder<T>(value, v => { });
        }
    }

    public class Holder<T> : IDisposable {
        private readonly Action<T> _dispose;

        public T Value { get; }

        public Holder(T value, Action<T> dispose) {
            _dispose = dispose;
            Value = value;
        }

        public void Dispose() {
            _dispose.Invoke(Value);
        }
    }

    public class ReleasedEventArgs<T> : EventArgs {
        public ReleasedEventArgs(T value) {
            Value = value;
        }

        public T Value { get; }
    }

    public class HoldedList : HoldedList<object> {
        public HoldedList(int capacity) : base(capacity) { }

        /// <summary>
        /// Use Get() instead!
        /// </summary>
        public new IDisposable Get([CanBeNull] object value) {
            return Get();
        }

        [NotNull]
        public IDisposable Get() {
            return base.Get(new object());
        }
    }

    public class HoldedList<T> : IReadOnlyList<T> {
        private readonly List<T> _holded;

        public HoldedList(int capacity) {
            _holded = new List<T>(capacity);
        }

        [ContractAnnotation(@"value: null => null; value: notnull => notnull")]
        public Holder<T> Get([CanBeNull] T value) {
            if (ReferenceEquals(value, null)) return null;

            var holder = new Holder<T>(value, Release);
            _holded.Add(value);
            return holder;
        }

        public event EventHandler<ReleasedEventArgs<T>> Released; 

        private void Release(T obj) {
            for (var i = 0; i < _holded.Count; i++) {
                if (ReferenceEquals(obj, _holded[i])) {
                    _holded.RemoveAt(i);
                    Released?.Invoke(this, new ReleasedEventArgs<T>(obj));
                    return;
                }
            }
        }

        public bool Contains(T obj) {
            for (var i = 0; i < _holded.Count; i++) {
                if (ReferenceEquals(obj, _holded[i])) {
                    return true;
                }
            }

            return false;
        }

        public IEnumerator<T> GetEnumerator() {
            return _holded.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public int Count => _holded.Count;

        public T this[int index] => _holded[index];
    }
}