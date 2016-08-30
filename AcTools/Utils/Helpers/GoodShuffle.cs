using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public class GoodShuffle {
        public static GoodShuffle<T> Get<T>(IList<T> list) {
            return new GoodShuffle<T>(list);
        }

        public static GoodShuffle<T> Get<T>(IEnumerable<T> list) {
            return new GoodShuffle<T>(list.ToList());
        }
    }

    public class GoodShuffle<T> : GoodShuffle, IEnumerable<T> {
        private readonly IList<T> _list;
        private int[] _buffer;
        private int _bufferPosition;

        public int Limit { get; private set; }

        internal GoodShuffle(IList<T> list) {
            _list = list;
            Limit = list.Count * 1000;
            Shuffle();
        }

        private bool _ignoreItem;
        private T _ignoredItem;
        public void IgnoreOnce(T item) {
            _ignoreItem = true;
            _ignoredItem = item;
        }

        private void Shuffle() {
            var size = _list.Count;
            _buffer = new int[size];

            for (var i = 0; i < _buffer.Length; i++) {
                _buffer[i] = i % _list.Count;
            }

            for (var i = 0; i < _buffer.Length; i++) {
                var n = MathUtils.Random(_buffer.Length - 1);
                var v = _buffer[n];
                _buffer[n] = _buffer[i];
                _buffer[i] = v;
            }

            _bufferPosition = 0;
            _ignoredItem = default(T);
            _ignoreItem = false;
        }
        
        public T GetNext() {
            if (_buffer.Length == 0) return default(T);

            while (true) {
                if (_bufferPosition >= _buffer.Length) Shuffle();

                var item = _list[_buffer[_bufferPosition++]];
                if (_ignoreItem && EqualityComparer<T>.Default.Equals(_ignoredItem, item)) continue;

                return item;
            }
        }

        public void RemoveLimit() {
            Limit = int.MaxValue;
        }
        
        public T Next => GetNext();

        public IEnumerator<T> GetEnumerator() {
            for (var i = 0; i < Limit; i++) {
                yield return GetNext();
            }
            throw new InvalidOperationException("Limit exceeded");
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
