using System;
using System.Collections.Generic;
using System.Text;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils.Physics {
    public struct LutPoint {
        public double X { get; }
        public double Y { get; }

        public LutPoint(double x, double y) {
            X = x;
            Y = y;
        }

        public override string ToString() {
            return $"({X.ToInvariantString()}, {Y.ToInvariantString()})";
        }

        public bool Equals(LutPoint other) {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is LutPoint point && Equals(point);
        }

        public override int GetHashCode() {
            unchecked {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }
    }

    public static class LutExtension {
        public static Lut ToLut(this IEnumerable<LutPoint> lut) {
            return new Lut(lut);
        }
    }

    public class Lut : List<LutPoint> {
        public Lut() {}

        public Lut(int capacity) : base(capacity) { }

        public Lut(IEnumerable<LutPoint> collection) : base(collection) {}

        [Pure, NotNull]
        public Lut Transform(Func<LutPoint, double> fn) {
            var result = new Lut(Count);
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                result.Add(new LutPoint(point.X, fn(point)));
            }
            return result;
        }

        public void TransformSelf(Func<LutPoint, double> fn) {
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                this[i] = new LutPoint(point.X, fn(point));
            }
        }

        public Lut Transform(Func<LutPoint, LutPoint> fn) {
            var result = new Lut(Count);
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                result.Add(fn(point));
            }
            return result;
        }

        public void TransformSelf(Func<LutPoint, LutPoint> fn) {
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                this[i] = fn(point);
            }
        }

        public Lut TransformHorizontally(Func<LutPoint, double> fn) {
            var result = new Lut(Count);
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                result.Add(new LutPoint(fn(point), point.Y));
            }
            return result;
        }

        public void TransformHorizontallySelf(Func<LutPoint, double> fn) {
            for (var i = 0; i < Count; i++) {
                var point = this[i];
                this[i] = new LutPoint(fn(point), point.Y);
            }
        }

        [Pure]
        public double InterpolateLinear(double x) {
            var current = default(LutPoint);
            var previous = default(LutPoint);

            for (var i = 0; i < Count; i++) {
                current = this[i];
                if (current.X > x) {
                    return i == 0 ? current.Y : previous.Y + (x - previous.X) * (current.Y - previous.Y) / (current.X - previous.X);
                }

                previous = current;
            }

            return current.Y;
        }

        public double MinX { get; private set; }
        public double MaxX { get; private set; }
        public double MinY { get; private set; }
        public double MaxY { get; private set; }

        /// <summary>
        /// Updates values of MinX, MaxX, MinY, MaxY. Since <see cref="Lut"/> object is mutable,
        /// use it before accessing to them.
        /// </summary>
        public void UpdateBoundingBox() {
            if (Count == 0) {
                MinX = MaxX = MinY = MaxY = double.NaN;
                return;
            }

            var first = this[0];
            MinX = MaxX = first.X;
            MinY = MaxY = first.Y;

            for (var i = 1; i < Count; i++) {
                var p = this[i];
                if (p.X < MinX) MinX = p.X;
                if (p.X > MaxX) MaxX = p.X;
                if (p.Y < MinY) MinY = p.Y;
                if (p.Y > MaxY) MaxY = p.Y;
            }
        }

        private int FindLeft(double x) {
            for (var i = 0; i < Count; i++) {
                if (this[i].X > x) return i - 1;
            }
            return Count - 1;
        }

        private LutPoint GetClamped(int i) {
            return this[i < 0 ? 0 : i >= Count ? Count - 1 : i];
        }

        private double GetTangent(int k) {
            var kc = GetClamped(k);
            return (GetTangent(GetClamped(k - 1), kc) + GetTangent(kc, GetClamped(k + 1))) / 2d;
        }

        private double GetTangent(LutPoint p, LutPoint n) {
            if (n.X == p.X) return 0d;
            return (n.Y - p.Y) / (n.X - p.X);
        }

        [Pure]
        public double InterpolateCubic(double x) {
            if (Count == 0) return 0d;

            var s = this[0];
            var e = this[Count - 1];
            if (x <= s.X) return s.Y;
            if (x >= e.X) return e.Y;

            var k = FindLeft(x);
            var p1 = GetClamped(k);
            var p2 = GetClamped(k + 1);
            var di = (p2.X - p1.X);
            var t1 = (x - p1.X) / di;
            var t2 = t1 * t1;
            var t3 = t1 * t2;

            return (2 * t3 - 3 * t2 + 1) * p1.Y + (t3 - 2 * t2 + t1) * GetTangent(k) * di +
                (-2 * t3 + 3 * t2) * p2.Y + (t3 - t2) * GetTangent(k + 1) * di;
        }

        [Pure, NotNull]
        public Lut Optimize(double threshold = 0.07) {
            if (Count < 3) return new Lut(this);

            var optimized = new Lut { this[0] };
            var offset = 1;
            for (var i = 1; i < Count - 1; i++) {
                var current = this[i];
                var coefficient = 1 - GetTangent(current, this[i + 1]) /
                        GetTangent(this[i - offset], current);
                if (double.IsInfinity(coefficient) || double.IsNaN(coefficient) || Math.Abs(coefficient) > threshold) {
                    optimized.Add(current);
                    offset = 1;
                } else {
                    offset++;
                }
            }

            optimized.Add(this[Count - 1]);
            return optimized;
        }

        public static bool IsInlineValue([NotNull] string value) {
            return !value.EndsWith(".lut", StringComparison.OrdinalIgnoreCase) &&
                    (value.IndexOf('=') != -1 || value.IndexOf('|') != -1);
        }

        /// <summary>
        /// Parse lut value from INI-file, something like “(|0=0.8|1000=0.9|)”.
        /// Files are parsed by <see cref="LutDataFile" /> type (also, it warns user about syntax errors).
        /// </summary>
        /// <param name="value">Lut value.</param>
        /// <returns>Parsed curve.</returns>
        [Pure, NotNull]
        public static Lut FromValue([NotNull, LocalizationRequired(false)] string value) {
            var capacity = 0;
            for (var i = 1; i < value.Length; i++) {
                if (value[i] == '=') capacity++;
            }

            var result = new Lut(capacity);
            if (value.Length > 2 && value[0] == '(' && value[value.Length - 1] == ')') {
                var j = 1;
                double? key = null;
                for (var i = 1; i < value.Length; i++) {
                    switch (value[i]) {
                        case '|':
                        case ')':
                            if (i > j && key.HasValue) {
                                result.Add(new LutPoint(key.Value, FlexibleParser.ParseDouble(value.Substring(j, i - j), 0d)));
                                key = null;
                            }
                            j = i + 1;
                            break;

                        case '=':
                            if (i > j) {
                                key = FlexibleParser.ParseDouble(value.Substring(j, i - j), 0d);
                            }
                            j = i + 1;
                            break;
                    }
                }
            }

            return result;
        }

        public delegate void CallbackPoint(double x, double y);
        public delegate LutPoint SelectPoint(double x, double y);

        public void ForEach(CallbackPoint callback, int detalization = 100, bool linearInterpolation = true) {
            UpdateBoundingBox();
            var startFrom = MinX;
            var limit = MaxX;
            var previousPoint = 0;
            var previousX = 0d;
            for (var i = 0; i <= detalization; i++) {
                var x = detalization == 0 ? limit : (limit - startFrom) * i / detalization + startFrom;

                for (var j = previousPoint; j < Count; j++) {
                    var p = this[j];

                    if (p.X > x) {
                        previousPoint = j > 0 ? j - 1 : 0;
                        break;
                    }

                    if ((i == 0 || p.X > previousX) && p.X < x) {
                        callback(p.X, p.Y);
                    }
                }

                callback(x, linearInterpolation ? InterpolateLinear(x) : InterpolateCubic(x));
                previousX = x;
            }
        }

        public Lut Select(SelectPoint callback, int detalization = 100, bool linearInterpolation = true) {
            var result = new Lut();
            ForEach((x, y) => {
                result.Add(callback(x, y));
            }, detalization, linearInterpolation);
            return result;
        }

        [NotNull, Pure]
        public Lut ScaleTo(double maxY) {
            UpdateBoundingBox();
            var multipler = Math.Abs(MaxY) < 0.001 ? 1.0 : maxY / MaxY;
            return ScaleBy(multipler);
        }

        [NotNull, Pure]
        public Lut ScaleBy(double multipler) {
            return new Lut(Transform(x => x.Y * multipler));
        }

        public void ScaleToSelf(double maxY) {
            UpdateBoundingBox();
            var multipler = Math.Abs(MaxY) < 0.001 ? 1.0 : maxY / MaxY;
            ScaleBySelf(multipler);
        }

        public void ScaleBySelf(double multipler) {
            TransformSelf(x => x.Y * multipler);
        }

        [NotNull, Pure]
        public Lut ScaleHorizontallyTo(double maxY) {
            UpdateBoundingBox();
            var multipler = Math.Abs(MaxX) < 0.001 ? 1.0 : maxY / MaxX;
            return ScaleHorizontallyBy(multipler);
        }

        [NotNull, Pure]
        public Lut ScaleHorizontallyBy(double multipler) {
            return new Lut(TransformHorizontally(x => x.X * multipler));
        }

        public void ScaleHorizontallyToSelf(double maxY) {
            UpdateBoundingBox();
            var multipler = Math.Abs(MaxX) < 0.001 ? 1.0 : maxY / MaxX;
            ScaleHorizontallyBySelf(multipler);
        }

        public void ScaleHorizontallyBySelf(double multipler) {
            TransformHorizontallySelf(x => x.X * multipler);
        }

        [Pure]
        public override string ToString() {
            var result = new StringBuilder(Count * 4 + 2);
            result.Append("(|");

            for (var i = 0; i < Count; i++) {
                var p = this[i];
                result.Append(p.X.ToInvariantString());
                result.Append('=');
                result.Append(p.Y.ToInvariantString());
                result.Append('|');
            }

            result.Append(')');
            return result.ToString();
        }
    }
}
