using System;
using AcTools.Utils.Helpers;

namespace AcTools.Utils {
    public static class MathUtils {
        public static double Pow(this double v, double p) => Math.Pow(v, p);
        public static float Pow(this float v, float p) => (float)Math.Pow(v, p);

        public static double Sqrt(this double v) => Math.Sqrt(v);
        public static float Sqrt(this float v) => (float)Math.Sqrt(v);

        public static double Acos(this double v) => Math.Acos(v);
        public static float Acos(this float v) => (float)Math.Acos(v);

        public static double Sin(this double v) => Math.Sin(v);
        public static float Sin(this float v) => (float)Math.Sin(v);

        public static double Cos(this double v) => Math.Cos(v);
        public static float Cos(this float v) => (float)Math.Cos(v);

        public static int Abs(this int v) => v < 0 ? -v : v;
        public static double Abs(this double v) => v < 0d ? -v : v;
        public static float Abs(this float v) => v < 0f ? -v : v;

        public static bool IsFinite(this double v) => !double.IsInfinity(v) && !double.IsNaN(v);
        public static bool IsFinite(this float v) => !float.IsInfinity(v) && !float.IsNaN(v);

        public static int Clamp(this int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp(this float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static double Clamp(this double v, double min, double max) => v < min ? min : v > max ? max : v;
        public static TimeSpan Clamp(this TimeSpan v, TimeSpan min, TimeSpan max) => v < min ? min : v > max ? max : v;

        public static byte ClampToByte(this double v) => (byte)(v < 0d ? 0 : v > 255d ? 255 : v);
        public static byte ClampToByte(this int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        public static float Saturate(this float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        public static double Saturate(this double value) => value < 0d ? 0d : value > 1d ? 1d : value;

        // [Obsolete] TODO
        public static int ToIntPercentage(this double value) => (100 * value).RoundToInt();

        // [Obsolete] TODO
        public static double ToDoublePercentage(this int value) => 0.01 * value;

        public static int RoundToInt(this double value) => (int)Math.Floor(value);

        /// <summary>
        /// For example: Round(0.342, 0.05) → 0.35.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static double Round(this double value, double precision = 1d) {
            if (Equals(precision, 0d)) return value;
            return Math.Round(value / precision) * precision;
        }

        /// <summary>
        /// For example: Round(0.342, 0.05) → 0.30.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static double Floor(this double value, double precision = 1d) {
            if (Equals(precision, 0d)) return value;
            return Math.Floor(value / precision) * precision;
        }

        /// <summary>
        /// For example: Round(0.327, 0.05) → 0.35.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static double Ceiling(this double value, double precision = 1d) {
            if (Equals(precision, 0d)) return value;
            return Math.Ceiling(value / precision) * precision;
        }

        /// <summary>
        /// For example: Round(0.342, 0.05) → 0.35.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static float Round(this float value, float precision = 1f) {
            if (Equals(precision, 0f)) return value;
            return (float)(Math.Round(value / precision) * precision);
        }

        /// <summary>
        /// For example: Round(340, 25) → 350.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int Round(this int value, int precision = 1) {
            if (Equals(precision, 0)) return value;
            return (int)(Math.Round((double)value / precision) * precision);
        }

        /// <summary>
        /// For example: Round(340, 25) → 325.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int Floor(this int value, int precision = 1) {
            if (Equals(precision, 0)) return value;
            return (int)(Math.Floor((double)value / precision) * precision);
        }

        /// <summary>
        /// For example: Round(327, 25) → 350.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int Ceiling(this int value, int precision = 1) {
            if (Equals(precision, 0)) return value;
            return (int)(Math.Ceiling((double)value / precision) * precision);
        }

        /// <summary>
        /// Checks if double is equal to another double considering another double’s precision. For example:
        /// RoughlyEquals(15.342, 15.34) → true; RoughlyEquals(15.34, 15.342) → false.
        /// </summary>
        /// <param name="value">Value which will be compared</param>
        /// <param name="origin">Another value which will be compared and will define precision</param>
        /// <returns></returns>
        public static bool RoughlyEquals(this double value, double origin) {
            // Stupidest way. But if it works, is it so stupid? Yes, it is.
            var first = value.ToInvariantString();
            var second = origin.ToInvariantString();
            return first.Length > second.Length ? Equals(first.Substring(0, second.Length), second) : Equals(first, second);
        }

        [ThreadStatic]
        private static Random _random;

        public static Random RandomInstance => _random ?? (_random = new Random(Guid.NewGuid().GetHashCode()));

        public static int Random(int maxValueExclusive) => RandomInstance.Next(maxValueExclusive);

        public static int Random(int minValueInclusive, int maxValueExclusive) => RandomInstance.Next(minValueInclusive, maxValueExclusive);

        public static double Random() => RandomInstance.NextDouble();

        public static double Random(double maxValue) => RandomInstance.NextDouble() * maxValue;

        public static double Random(double minValue, double maxValue) => Random(maxValue - minValue) + minValue;

        public static float Random(float minValue, float maxValue) => (float)(Random(maxValue - minValue) + minValue);

        public static TimeSpan Max(this TimeSpan a, TimeSpan b) {
            return a > b ? a : b;
        }

        public static TimeSpan Min(this TimeSpan a, TimeSpan b) {
            return a < b ? a : b;
        }
    }
}