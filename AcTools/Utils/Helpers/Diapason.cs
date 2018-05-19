using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class Diapason {
        public static Diapason<int> CreateInt32([NotNull] string diapason) {
            return new IntDiapason(diapason);
        }

        public static Diapason<double> CreateDouble([NotNull] string diapason, bool roundSingle = true) {
            return new DoubleDiapason(diapason, roundSingle);
        }

        public static Diapason<int> CreateTime([NotNull] string diapason, bool roundSingle = true) {
            return new TimeDiapason(diapason, roundSingle);
        }

        private class IntDiapason : Diapason<int> {
            public IntDiapason([NotNull] string diapason) : base(diapason) { }

            protected override bool TryParse(string value, out int parsed) {
                return FlexibleParser.TryParseInt(value, out parsed);
            }

            protected override int GetDefaultStep() {
                return 1;
            }

            protected override IEnumerable<int> Range(int fromValue, int toValue, int step) {
                for (var i = fromValue; i <= toValue; i += step) {
                    yield return i;
                }
            }
        }

        private class DoubleDiapason : Diapason<double>, IEqualityComparer<double> {
            private readonly bool _roundSingle;

            public DoubleDiapason([NotNull] string diapason, bool roundSingle) : base(diapason) {
                _roundSingle = roundSingle;
            }

            protected override IEqualityComparer<double> GetEqualityComparer(string originalValue) {
                return this;
            }

            protected override bool TryParse(string value, out double parsed) {
                return FlexibleParser.TryParseDouble(value, out parsed);
            }

            protected override double GetDefaultStep() {
                return 1d;
            }

            protected override IEnumerable<double> Range(double fromValue, double toValue, double step) {
                for (var i = fromValue; i <= toValue; i += step) {
                    yield return i;
                }
            }

            bool IEqualityComparer<double>.Equals(double x, double y) {
                return _roundSingle ? x.RoughlyEquals(y) : x == y;
            }

            int IEqualityComparer<double>.GetHashCode(double obj) {
                return obj.GetHashCode();
            }
        }

        private class TimeDiapason : Diapason<int> {
            private readonly bool _roundSingle;

            public TimeDiapason([NotNull] string diapason, bool roundSingle) : base(diapason) {
                _roundSingle = roundSingle;
            }

            protected override bool TryParse(string value, out int parsed) {
                return FlexibleParser.TryParseTime(value, out parsed);
            }

            protected override int GetDefaultStep() {
                return 60;
            }

            protected override IEnumerable<int> Range(int fromValue, int toValue, int step) {
                for (var i = fromValue; i <= toValue; i += step) {
                    yield return i;
                }
            }

            protected override IEqualityComparer<int> GetEqualityComparer(string originalValue) {
                return _roundSingle ? new TimeEqualityComparer(originalValue) : base.GetEqualityComparer(originalValue);
            }

            private class TimeEqualityComparer : IEqualityComparer<int> {
                private readonly int _delimiters;

                public TimeEqualityComparer(string originalValue) {
                    _delimiters = originalValue.Count(':');
                }

                public bool Equals(int x, int y) {
                    return y.Equals(_delimiters == 2 ? x : _delimiters == 1 ? x.Floor(60) : x.Floor(60 * 60));
                }

                public int GetHashCode(int obj) {
                    return obj.GetHashCode();
                }
            }
        }
    }

    public abstract class Diapason<T> : IEnumerable<T> {
        private readonly string _diapason;

        protected Diapason([NotNull] string diapason) {
            _diapason = diapason ?? throw new ArgumentNullException(nameof(diapason));
        }

        private bool _limitsSet;
        private T _minimum, _maximum;

        public Diapason<T> SetLimits(T minimum, T maximum) {
            _limitsSet = true;
            _minimum = minimum;
            _maximum = maximum;
            return this;
        }

        private Piece[] _pieces;

        protected abstract bool TryParse(string value, out T parsed);
        protected abstract T GetDefaultStep();
        protected abstract IEnumerable<T> Range(T fromValue, T toValue, T step);

        protected virtual IComparer<T> GetComparer() {
            return Comparer<T>.Default;
        }

        protected virtual IEqualityComparer<T> GetEqualityComparer(string originalValue) {
            return EqualityComparer<T>.Default;
        }

        private void Prepare() {
            if (_pieces != null) return;

            var comparer = GetComparer();
            _pieces = _diapason.Split(',', ';').Select(x => GetPiece(x.Trim())).NonNull().ToArray();

            T ClampFrom(T fromValue) {
                return _limitsSet && comparer.Compare(fromValue, _minimum) < 0 ? _minimum : fromValue;
            }

            T ClampTo(T toValue) {
                return _limitsSet && comparer.Compare(toValue, _maximum) > 0 ? _maximum : toValue;
            }

            Piece GetPiece(string part) {
                var n = part.IndexOfAny(new[] { '-', '…', '—', '–' });
                if (n == 0) {
                    var m = part.IndexOfAny(new[] { '-', '…', '—', '–' }, n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                if (n > 0 && n < part.Length - 1) {
                    // "x-y"
                    if (TryParse(part.Substring(0, n), out var fromValue) && TryParse(part.Substring(n + 1), out var toValue)) {
                        return new ClosedRangePiece(ClampFrom(fromValue), ClampTo(toValue), comparer);
                    }
                } else if (n < 0) {
                    // "x"
                    if (TryParse(part, out var value)) {
                        return new SingleValueRangePiece(value, GetEqualityComparer(part));
                    }
                } else if (part.Length == 1) {
                    // "-"
                    if (_limitsSet) {
                        return new ClosedRangePiece(_minimum, _maximum, comparer);
                    }
                    return new OpenRangePiece();
                } else if (n == part.Length - 1) {
                    // "x-"
                    if (TryParse(part.Substring(0, n), out var fromValue)) {
                        if (_limitsSet) {
                            return new ClosedRangePiece(ClampFrom(fromValue), _maximum, comparer);
                        }

                        return new OpenEndRangePiece(fromValue, comparer);
                    }
                } else {
                    // "-x"
                    if (TryParse(part.Substring(n + 1), out var toValue)) {
                        if (_limitsSet) {
                            return new ClosedRangePiece(_minimum, ClampTo(toValue), comparer);
                        }

                        return new OpenBeginningRangePiece(toValue, comparer);
                    }
                }

                return null;
            }
        }

        private class Ranger {
            private readonly Func<T, T, IEnumerable<T>> _fn;

            public Ranger(Func<T, T, IEnumerable<T>> fn) {
                _fn = fn;
            }

            public IEnumerable<T> Range(T fromValue, T toValue) {
                return _fn(fromValue, toValue);
            }
        }

        private abstract class Piece {
            public abstract bool Contains(T value);
            public abstract IEnumerable<T> GetValues(Ranger range);
        }

        private class ClosedRangePiece : Piece {
            private readonly T _fromValue, _toValue;
            private readonly IComparer<T> _comparer;

            public ClosedRangePiece(T fromValue, T toValue, IComparer<T> comparer) {
                _fromValue = fromValue;
                _toValue = toValue;
                _comparer = comparer;
            }

            public override bool Contains(T value) {
                return _comparer.Compare(value, _fromValue) >= 0 && _comparer.Compare(value, _toValue) <= 0;
            }

            public override IEnumerable<T> GetValues(Ranger range) {
                return range.Range(_fromValue, _toValue);
            }
        }

        private class OpenRangePiece : Piece {
            public override bool Contains(T value) {
                return true;
            }

            public override IEnumerable<T> GetValues(Ranger range) {
                throw new NotSupportedException("Can’t enumerate open range");
            }
        }

        private class OpenEndRangePiece : Piece {
            private readonly T _fromValue;
            private readonly IComparer<T> _comparer;

            public OpenEndRangePiece(T fromValue, IComparer<T> comparer) {
                _fromValue = fromValue;
                _comparer = comparer;
            }

            public override bool Contains(T value) {
                return _comparer.Compare(value, _fromValue) >= 0;
            }

            public override IEnumerable<T> GetValues(Ranger range) {
                throw new NotSupportedException("Can’t enumerate open range");
            }
        }

        private class OpenBeginningRangePiece : Piece {
            private readonly T _toValue;
            private readonly IComparer<T> _comparer;

            public OpenBeginningRangePiece(T toValue, IComparer<T> comparer) {
                _toValue = toValue;
                _comparer = comparer;
            }

            public override bool Contains(T value) {
                return _comparer.Compare(value, _toValue) <= 0;
            }

            public override IEnumerable<T> GetValues(Ranger range) {
                throw new NotSupportedException("Can’t enumerate open range");
            }
        }

        private class SingleValueRangePiece : Piece {
            private readonly T _value;
            private readonly IEqualityComparer<T> _comparer;

            public SingleValueRangePiece(T value, IEqualityComparer<T> comparer) {
                _value = value;
                _comparer = comparer;
            }

            public override bool Contains(T value) {
                return _comparer.Equals(value, _value);
            }

            public override IEnumerable<T> GetValues(Ranger range) {
                return range.Range(_value, _value);
            }
        }

        public bool Contains(T value) {
            Prepare();

            var pieces = _pieces;
            for (var i = pieces.Length - 1; i >= 0; i--) {
                if (pieces[i].Contains(value)) return true;
            }

            return false;
        }

        public IEnumerable<T> Range(T step) {
            Prepare();
            var ranger = new Ranger((f, t) => Range(f, t, step));
            return _pieces.SelectMany(x => x.GetValues(ranger));
        }

        public IEnumerator<T> GetEnumerator() {
            return Range(GetDefaultStep()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}