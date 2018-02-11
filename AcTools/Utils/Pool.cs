using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public class PoolConstruct<T> {
        private readonly Func<T> _construct;
        private readonly List<T> _stored = new List<T>(10);

        public PoolConstruct([NotNull] Func<T> construct) {
            _construct = construct;
        }

        public T Get() {
            lock (_stored) {
                var stored = _stored;
                var count = stored.Count;
                if (count == 0) return _construct.Invoke();

                var result = stored[count - 1];
                stored.RemoveAt(count - 1);
                return result;
            }
        }

        public void AddAll(ICollection<T> collection) {
            lock (_stored) {
                _stored.AddRange(collection);
            }
            collection.Clear();
        }

        public void AddFrom(IList<T> collection, int position) {
            lock (_stored) {
                _stored.Add(collection[position]);
            }
            collection.RemoveAt(position);
        }

        public void AddFrom(ICollection<T> collection, T item) {
            lock (_stored) {
                _stored.Add(item);
            }
            collection.Remove(item);
        }

        public void AddFrom(IList collection, int position) {
            lock (_stored) {
                _stored.Add((T)collection[position]);
            }
            collection.RemoveAt(position);
        }

        public void AddFrom(IList collection, T item) {
            lock (_stored) {
                _stored.Add(item);
            }
            collection.Remove(item);
        }
    }

    public class Pool<T> : PoolConstruct<T> where T : new() {
        public Pool() : base(() => new T()) { }
    }
}