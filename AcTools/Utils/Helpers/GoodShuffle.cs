using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AcTools.Utils.Helpers {
    public class GoodShuffle {
        public static GoodShuffle<T> Get<T>(List<T> list) {
            return new GoodShuffle<T>(list);
        }

        public static GoodShuffle<T> Get<T>(IEnumerable<T> list) {
            return new GoodShuffle<T>(list.ToList());
        }
    }

    public class GoodShuffle<T> : BaseShuffle<T> {
        public GoodShuffle(List<T> list) : base(list) { }

        protected override void Shuffle(int[] buffer) {
            for (var i = 0; i < buffer.Length; i++) {
                buffer[i] = i;
            }

            ShuffleArray(buffer);
        }

        protected static void ShuffleArray(int[] list) {
            for (var i = 0; i < list.Length; i++) {
                var n = MathUtils.Random(list.Length);
                if (n == i) continue;
                var w = list[i];
                list[i] = list[n];
                list[n] = w;
            }
        }
    }

    public class LimitedShuffle {
        public static bool OptionUseKuhn = true;

        public static LimitedShuffle<T> Get<T>(List<T> list, double randomization) {
            return new LimitedShuffle<T>(list, randomization);
        }

        public static LimitedShuffle<T> Get<T>(IEnumerable<T> list, double randomization) {
            return new LimitedShuffle<T>(list.ToList(), randomization);
        }
    }

    public class LimitedShuffle<T> : GoodShuffle<T> {
        private readonly double _randomization;

        public LimitedShuffle(List<T> list, double randomization) : base(list) {
            _randomization = randomization;
        }

        private static void Shuffle(int[] buffer, int limit) {
            for (var i = 0; i < buffer.Length; i++) {
                buffer[i] = i;
            }

            for (var i = buffer.Length - 1; i >= 0; i--) {
                var t = buffer[i];
                var a = Math.Max(t - limit, 0);
                var b = Math.Min(t + limit, buffer.Length - 1);

                var n = MathUtils.Random(a, b + 1);
                if (n != i) {
                    var ai = Math.Max(i - limit, 0);
                    var bi = Math.Min(i + limit, buffer.Length - 1);

                    var v = buffer[n];
                    if (v >= ai && v <= bi) {
                        buffer[i] = buffer[n];
                        buffer[n] = t;
                    }
                }
            }
        }
        private static bool TryKuhn(bool[] used, int[][] g, int[] mt, int v) {
            if (used[v]) return false;
            used[v] = true;
            for (var i = 0; i < g[v].Length; i++) {
                var to = g[v][i];
                if (mt[to] == -1 || TryKuhn(used, g, mt, mt[to])) {
                    mt[to] = v;
                    return true;
                }
            }
            return false;
        }

        private static void ShuffleKuhn(int[] buffer, int limit) {
            var size = buffer.Length;

            var g = new int[size][];
            for (var i = 0; i < size; i++) {
                var f = Math.Max(0, i - limit);
                var t = Math.Min(size - 1, i + limit);
                g[i] = new int[t - f + 1];
                for (var j = f; j <= t; j++) {
                    g[i][j - f] = j;
                }
                ShuffleArray(g[i]);
            }

            var mt = new int[size];
            for (var i = 0; i < size; i++) {
                mt[i] = -1;
            }

            for (var i = 0; i < size; i++) {
                TryKuhn(new bool[size], g, mt, i);
            }

            for (var i = 0; i < size; i++) {
                if (mt[i] != -1) {
                    buffer[mt[i]] = i;
                }
            }
        }

        protected override void Shuffle(int[] buffer) {
            var offset = (int)Math.Round(_randomization * (buffer.Length - 1));

            if (offset <= 0) {
                /* do not shuffle anything */
                for (var i = 0; i < buffer.Length; i++) {
                    buffer[i] = i;
                }
                return;
            }

            if (offset >= buffer.Length - 1) {
                /* maximum shuffling */
                base.Shuffle(buffer);
                return;
            }

            if (LimitedShuffle.OptionUseKuhn) {
                ShuffleKuhn(buffer, offset);
            } else {
                for (var k = 0; k < 1000; k++) {
                    Shuffle(buffer, offset);
                    for (var i = 0; i < buffer.Length; i++) {
                        var delta = Math.Abs(i - buffer[i]);
                        if (delta > offset) {
                            goto NextIteration;
                        }
                    }

                    if (k > 0) {
                        Debug.WriteLine("Attempts: " + k);
                    }

                    return;
                    NextIteration:
                    { }
                }

                Debug.WriteLine("Out! " + string.Join(",", buffer));
                for (var i = 0; i < buffer.Length; i++) {
                    buffer[i] = i;
                }
            }
        }
    }

    public abstract class BaseShuffle<T> : GoodShuffle, IEnumerable<T> {
        public IReadOnlyList<T> OriginalList => _list;

        private readonly List<T> _list;
        private int[] _buffer;
        private int _bufferPosition;

        public int Size { get; }
        public int Limit { get; private set; }

        internal BaseShuffle(List<T> list) {
            if (list.Count == 0) {
                throw new ArgumentException("Value cannot be an empty collection.", nameof(list));
            }

            _list = list;
            Limit = _list.Count * 1000;
            Size = _list.Count;
        }

        private bool _ignoreItem;
        private T _ignoredItem;
        public void IgnoreOnce(T item) {
            _ignoreItem = true;
            _ignoredItem = item;
        }

        protected abstract void Shuffle(int[] buffer);

        private void Shuffle() {
            if (_buffer == null) {
                _buffer = new int[_list.Count];
            }

            Shuffle(_buffer);
            _bufferPosition = 0;
        }

        private void Prepare() {
            if (_buffer == null || _bufferPosition >= _buffer.Length) {
                Shuffle();
            }
        }

        public T GetNext() {
            if (_list.Count == 0) return default(T);

            while (true) {
                Prepare();
                var item = _list[_buffer[_bufferPosition++]];
                if (_ignoreItem && Equals(_ignoredItem, item)) {
                    _ignoredItem = default(T);
                    _ignoreItem = false;
                    continue;
                }

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
