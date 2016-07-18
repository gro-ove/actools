using System;

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

        public static double Abs(this double v) => v < 0d ? -v : v;
        public static float Abs(this float v) => v < 0f ? -v : v;

        public static bool IsFinite(this double v) => !double.IsInfinity(v) && !double.IsNaN(v);
        public static bool IsFinite(this float v) => !float.IsInfinity(v) && !float.IsNaN(v);

        public static int Clamp(this int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp(this float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static double Clamp(this double v, double min, double max) => v < min ? min : v > max ? max : v;

        public static byte ClampToByte(this double v) => (byte)(v < 0d ? 0 : v > 255d ? 255 : v);
        public static byte ClampToByte(this int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        public static float Saturate(this float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        public static double Saturate(this double value) => value < 0d ? 0d : value > 1d ? 1d : value;

        public static int ToIntPercentage(this double value) => (100 * value).RoundToInt();

        public static double ToDoublePercentage(this int value) => 0.01 * value;

        public static int RoundToInt(this double value) => (int)Math.Round(value);

        /// <summary>
        /// Round(0.342, 0.05) → 0.35
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static double Round(this double value, double precision) {
            return Math.Round(value / precision) * precision;
        }

        /// <summary>
        /// Round(340, 25) → 325
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static int Round(this int value, int precision) {
            return (int)(Math.Round((double)value / precision) * precision);
        }

        public static float[] MatrixInverse(this float[] matrix) {
            return Matrix.Create(matrix).Invert().ToArray();
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