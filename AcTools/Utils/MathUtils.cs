using System;

namespace AcTools.Utils {
    public static class MathUtils {
        public static float SqrtF(float v) {
            return (float)Math.Sqrt(v);
        }

        public static float AcosF(float v) {
            return (float)Math.Acos(v);
        }

        public static float SinF(float v) {
            return (float)Math.Sin(v);
        }

        public static float CosF(float v) {
            return (float)Math.Cos(v);
        }

        public static float AbsF(float v) {
            return v < 0.0 ? -v : v;
        }

        public static bool IsFinite(float v) {
            return !float.IsInfinity(v) && !float.IsNaN(v);
        }

        public static bool IsFloatsAreEqual(float af, float bf, float maxDiff) {
            var d = af - bf;
            return d < maxDiff && d > -maxDiff;
        }

        public static bool IsFinite(double v) {
            return !double.IsInfinity(v) && !double.IsNaN(v);
        }

        public static int Clamp(int value, int minimum, int maximum) {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        public static double Clamp(double value, double minimum, double maximum) {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        public static float[] MatrixInverse(float[] matrix) {
            return Matrix.Create(matrix).Invert().ToArray();
        }

        /// <summary>
        /// Round(0.342, 0.05) → 0.35
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static double Round(double value, double precision) {
            return Math.Round(value / precision) * precision;
        }

        [ThreadStatic]
        private static Random _random;

        public static Random RandomInstance => _random ?? (_random = new Random(Guid.NewGuid().GetHashCode()));

        public static int Random(int maxValue) => RandomInstance.Next(maxValue);

        public static int Random(int minValueInclusive, int maxValueExclusive) => RandomInstance.Next(minValueInclusive, maxValueExclusive);

        public static double Random() => RandomInstance.NextDouble();

        public static double Random(double maxValue) => RandomInstance.NextDouble() * maxValue;
    }
}