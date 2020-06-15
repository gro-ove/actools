using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

            protected override void GetDefaultLimits(out int minimum, out int maximum) {
                minimum = int.MinValue;
                maximum = int.MaxValue;
            }

            protected IntDiapason([NotNull] List<Piece> pieces) : base(pieces) { }

            protected override Diapason<int> CreateNew(List<Piece> pieces) {
                return new IntDiapason(pieces);
            }

            protected override bool TryParse(string value, out int parsed) {
                return FlexibleParser.TryParseInt(value, out parsed);
            }

            protected override int GetDefaultStep() {
                return 1;
            }

            protected override IEnumerable<int> Range(int fromValue, int toValue, int step) {
                if (fromValue == int.MinValue) {
                    throw new NotSupportedException("Can’t enumerate open diapasons");
                }

                for (var i = fromValue; i <= toValue; i += step) {
                    yield return i;
                }
            }

            protected override int GetDistance(int a, int b) {
                return (a - b).Abs();
            }
        }

        private class DoubleDiapason : Diapason<double> {
            private static readonly Regex FloatyPart = new Regex(@"\.\d+", RegexOptions.Compiled);

            private readonly bool _roundSingle;

            public DoubleDiapason([NotNull] string diapason, bool roundSingle) : base(diapason) {
                _roundSingle = roundSingle;
            }

            private DoubleDiapason([NotNull] List<Piece> pieces) : base(pieces) { }

            protected override Diapason<double> CreateNew(List<Piece> pieces) {
                return new DoubleDiapason(pieces);
            }

            protected override void GetDefaultLimits(out double minimum, out double maximum) {
                minimum = double.NegativeInfinity;
                maximum = double.PositiveInfinity;
            }

            protected override bool TryParse(string value, out double parsed) {
                return FlexibleParser.TryParseDouble(value, out parsed);
            }

            protected override double GetDefaultStep() {
                return 1d;
            }

            protected override IEnumerable<double> Range(double fromValue, double toValue, double step) {
                if (double.IsInfinity(fromValue)) {
                    throw new NotSupportedException("Can’t enumerate open diapasons");
                }

                for (var i = fromValue; i <= toValue; i += step) {
                    yield return i;
                }
            }

            protected override IEnumerable<Piece> TryToGetSingleValuePiece(string part, Clamper clamper) {
                if (!TryParse(part, out var value)) return null;

                if (_roundSingle && part.IndexOf('e') == -1 && part.IndexOf('E') == -1) {
                    var match = FloatyPart.Match(part);
                    if (match.Success) {
                        var accuracy = Math.Pow(0.1, match.Length - 1) * 0.99;
                        return new[] { new Piece(clamper.Clamp(value - accuracy), clamper.Clamp(value + accuracy)) };
                    }

                    return new[] { new Piece(clamper.Clamp(value), clamper.Clamp(value + 0.999)) };
                }

                return new[] { new Piece(clamper.Clamp(value)) };
            }

            protected override double GetDistance(double a, double b) {
                return (a - b).Abs();
            }
        }

        private class TimeDiapason : IntDiapason {
            private readonly bool _roundSingle;

            public TimeDiapason([NotNull] string diapason, bool roundSingle) : base(diapason) {
                _roundSingle = roundSingle;
            }

            private TimeDiapason([NotNull] List<Piece> pieces) : base(pieces) { }

            protected override Diapason<int> CreateNew(List<Piece> pieces) {
                return new TimeDiapason(pieces);
            }

            protected override void GetDefaultLimits(out int minimum, out int maximum) {
                minimum = 0;
                maximum = 24 * 60 * 60 - 60;
            }

            protected override bool TryParse(string value, out int parsed) {
                return FlexibleParser.TryParseTime(value, out parsed);
            }

            protected override int GetDefaultStep() {
                return 60;
            }

            protected override IEnumerable<Piece> CreatePiece(int fromValue, int toValue, Clamper clamper) {
                if (fromValue > toValue) {
                    return new[] {
                        new Piece(clamper.Minimum, clamper.Clamp(toValue)),
                        new Piece(clamper.Clamp(fromValue), clamper.Maximum),
                    };
                }

                return base.CreatePiece(fromValue, toValue, clamper);
            }

            protected override IEnumerable<Piece> TryToGetSingleValuePiece(string part, Clamper clamper) {
                if (!_roundSingle) return base.TryToGetSingleValuePiece(part, clamper);
                if (!TryParse(part, out var value)) return null;
                switch (part.Count(':')) {
                    case 0:
                        return new[] { new Piece(clamper.Clamp(value), clamper.Clamp(value + 60 * 60 - 1)) };
                    case 1:
                        return new[] { new Piece(clamper.Clamp(value), clamper.Clamp(value + 59)) };
                    default:
                        return new[] { new Piece(clamper.Clamp(value)) };
                }
            }
        }
    }

    public abstract class Diapason<T> : IEnumerable<T> {
        private readonly string _diapason;

        protected Diapason([NotNull] List<Piece> pieces) {
            _pieces = pieces;
        }

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

        private List<Piece> _pieces;
        private IComparer<T> _comparer;

        protected abstract void GetDefaultLimits(out T minimum, out T maximum);
        protected abstract bool TryParse(string value, out T parsed);
        protected abstract T GetDefaultStep();
        protected abstract Diapason<T> CreateNew(List<Piece> pieces);
        protected abstract IEnumerable<T> Range(T fromValue, T toValue, T step);
        protected abstract T GetDistance(T a, T b);

        protected virtual IComparer<T> GetComparer() {
            return Comparer<T>.Default;
        }

        protected class Clamper {
            private IComparer<T> _comparer;

            public readonly T Minimum;
            public readonly T Maximum;

            public Clamper(IComparer<T> comparer, T minimum, T maximum) {
                _comparer = comparer;
                Minimum = minimum;
                Maximum = maximum;
            }

            public T Clamp(T value) {
                return _comparer.Compare(value, Minimum) < 0 ? Minimum
                        : _comparer.Compare(value, Maximum) > 0 ? Maximum : value;
            }
        }

        [CanBeNull]
        protected virtual IEnumerable<Piece> CreatePiece(T fromValue, T toValue, [NotNull] Clamper clamper) {
            return _comparer.Compare(fromValue, toValue) > 0 ? null
                    : new[] { new Piece(clamper.Clamp(fromValue), clamper.Clamp(toValue)) };
        }

        [CanBeNull]
        protected virtual IEnumerable<Piece> TryToGetPiece([NotNull] string fromPart, [NotNull] string toPart, [NotNull] Clamper clamper) {
            if (TryParse(fromPart, out var fromValue) && TryParse(toPart, out var toValue)) {
                return CreatePiece(fromValue, toValue, clamper);
            }
            return null;
        }

        [CanBeNull]
        protected virtual IEnumerable<Piece> TryToGetSingleValuePiece([NotNull] string part, [NotNull] Clamper clamper) {
            return TryParse(part, out var value) ? new[] { new Piece(clamper.Clamp(value)) } : null;
        }

        private void Prepare() {
            if (_pieces != null) return;

            if (!_limitsSet) {
                GetDefaultLimits(out _minimum, out _maximum);
            }

            if (_comparer == null) {
                _comparer = GetComparer();
            }

            if (_comparer.Compare(_minimum, _maximum) > 0) {
                throw new NotSupportedException("Lower bound shouldn’t exceeded upper bound");
            }

            var clamper = new Clamper(_comparer, _minimum, _maximum);

            var splitted = _diapason.Split(',', ';');
            var pieces = new List<Piece>(splitted.Length);

            foreach (var s in splitted) {
                var p = GetPiece(s.Trim());
                switch (p) {
                    case null:
                        continue;
                    case Piece[] pp when pp.Length == 1:
                        if (pp[0] != null) {
                            pieces.Add(pp[0]);
                        }
                        break;
                    default:
                        pieces.AddRange(p.NonNull());
                        break;
                }
            }

            _pieces = pieces;
            Optimize(_pieces);

            IEnumerable<Piece> GetPiece(string part) {
                var n = part.IndexOfAny(new[] { '-', '…', '—', '–' });
                if (n == 0) {
                    var m = part.IndexOfAny(new[] { '-', '…', '—', '–' }, 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                if (n > 0 && n < part.Length - 1) {
                    // "x-y"
                    var piece = TryToGetPiece(part.Substring(0, n), part.Substring(n + 1), clamper);
                    if (piece != null) {
                        return piece;
                    }
                } else if (n < 0) {
                    // "x"
                    var piece = TryToGetSingleValuePiece(part, clamper);
                    if (piece != null) {
                        return piece;
                    }
                } else if (part.Length == 1) {
                    // "-"
                    return CreatePiece(_minimum, _maximum, clamper);
                } else if (n == part.Length - 1) {
                    // "x-"
                    if (TryParse(part.Substring(0, n), out var fromValue)) {
                        return CreatePiece(fromValue, _maximum, clamper);
                    }
                } else {
                    // "-x"
                    if (TryParse(part.Substring(n + 1), out var toValue)) {
                        return CreatePiece(_minimum, toValue, clamper);
                    }
                }

                return null;
            }
        }

        private void Optimize(List<Piece> input) {
            var comparer = _comparer ?? (_comparer = GetComparer());
            input.Sort((x, y) => comparer.Compare(x.FromValue, y.FromValue));
            for (var i = 0; i < input.Count - 1; i++) {
                var current = input[i];
                while (i < input.Count - 1) {
                    var next = input[i + 1];
                    if (comparer.Compare(current.ToValue, next.FromValue) >= 0) {
                        if (comparer.Compare(current.ToValue, next.ToValue) < 0) {
                            current.ToValue = next.ToValue;
                        }
                        input.RemoveAt(i + 1);
                    } else {
                        break;
                    }
                }
            }
            input.Capacity = input.Count;
        }

        public T FindClosest(T input) {
            return TryToFindClosest(input, out var closest) ? closest : input;
        }

        public bool TryToFindClosest(T input, out T closest) {
            Prepare();

            var pieces = _pieces;
            var comparer = _comparer;

            var minimumSet = false;
            var minimumDistance = default(T);
            var closestValue = default(T);

            for (var i = pieces.Count - 1; i >= 0; i--) {
                var piece = pieces[i];

                if (comparer.Compare(input, piece.FromValue) < 0) {
                    Check(piece.FromValue);
                } else if (comparer.Compare(input, piece.ToValue) > 0) {
                    Check(piece.ToValue);
                } else {
                    closest = input;
                    return true;
                }
            }

            if (!minimumSet) {
                closest = default;
                return false;
            }

            closest = closestValue;
            return true;

            void Check(T value) {
                var distance = GetDistance(input, value);
                if (!minimumSet || comparer.Compare(distance, minimumDistance) < 0) {
                    minimumSet = true;
                    minimumDistance = distance;
                    closestValue = value;
                }
            }
        }

        public void CombineWith([NotNull] Diapason<T> another) {
            Prepare();
            Pieces.AddRange(another.Pieces.Select(x => x.Clone()));
            Optimize(Pieces);
        }

        public void CombineWith([NotNull, ItemNotNull] params Diapason<T>[] another) {
            Prepare();
            Pieces.AddRange(another.SelectMany(x => x.Pieces).Select(x => x.Clone()));
            Optimize(Pieces);
        }

        public class Piece {
            public T FromValue, ToValue;

            public Piece(T value) {
                FromValue = value;
                ToValue = value;
            }

            public Piece(T fromValue, T toValue) {
                FromValue = fromValue;
                ToValue = toValue;
            }

            public override string ToString() {
                return Equals(FromValue, ToValue) ? $"{FromValue}" : $"{FromValue}-{ToValue}";
            }

            public Piece Clone() {
                return new Piece(FromValue, ToValue);
            }
        }

        [NotNull]
        public List<Piece> Pieces {
            get {
                Prepare();
                return _pieces;
            }
        }

        public bool Contains(T value) {
            Prepare();

            var pieces = _pieces;
            var comparer = _comparer;
            for (var i = pieces.Count - 1; i >= 0; i--) {
                var piece = pieces[i];
                if (comparer.Compare(value, piece.FromValue) >= 0 && comparer.Compare(value, piece.ToValue) <= 0) {
                    return true;
                }
            }

            return false;
        }

        [NotNull]
        public IEnumerable<T> Range(T step) {
            Prepare();
            return _pieces.SelectMany(x => Range(x.FromValue, x.ToValue, step));
        }

        public IEnumerator<T> GetEnumerator() {
            return Range(GetDefaultStep()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}