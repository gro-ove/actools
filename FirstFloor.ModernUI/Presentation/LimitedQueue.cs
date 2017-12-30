using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    internal class LimitedQueue<T> : List<T> {
        public int Limit { get; }

        public LimitedQueue(int limit, [NotNull] IEnumerable<T> collection) : base(collection.Take(limit)) {
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
        }

        public LimitedQueue(int limit) : base(limit) {
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
        }

        public void Enqueue(T obj) {
            if (Count >= Limit) {
                RemoveAt(0);
            }
            Add(obj);
        }

        public T Peek() {
            if (Count == 0) throw new InvalidOperationException("Empty queue");
            return this[Count - 1];
        }

        public T Dequeue() {
            var result = Peek();
            RemoveAt(Count - 1);
            return result;
        }

        public T DequeueOrDefault() {
            if (Count == 0) return default(T);
            var result = Peek();
            RemoveAt(Count - 1);
            return result;
        }
    }
}