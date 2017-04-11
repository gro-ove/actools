using System;
using System.Collections.Generic;

namespace AcTools.AiFile {
    public partial class AiLane {
        private static float Sqr(float v) {
            return v * v;
        }

        private static float LengthSqr(float[] a, float[] b) {
            return Sqr(a[0] - b[0]) + Sqr(a[1] - b[1]) + Sqr(a[2] - b[2]);
        }

        private static float Length(float[] a, float[] b) {
            return (float)Math.Sqrt(LengthSqr(a, b));
        }

        private static float GetT(double t, float[] p0, float[] p1, double alpha) {
            return (float)(Math.Pow(Math.Pow(LengthSqr(p1, p0), 0.5), alpha) + t);
        }

        private static float[] Mult(float s, float[] d) {
            return new[] { d[0] * s, d[1] * s, d[2] * s };
        }

        private static void MultSelf(float s, float[] d) {
            d[0] *= s;
            d[1] *= s;
            d[2] *= s;
        }

        private static float[] Sum(float[] a, float[] b) {
            return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] };
        }

        private static void SumSelf(float[] a, float[] b) {
            a[0] += b[0];
            a[1] += b[1];
            a[2] += b[2];
        }

        private static bool Equals(float[] a, float[] b) {
            return Equals(a[0], b[0]) && Equals(a[1], b[1]) && Equals(a[2], b[2]);
        }

        private static float[] Prev(float[] s0, float[] s1) {
            return new[] {
                s0[0] - 0.0001f * (s1[0] - s0[0]),
                s0[1] - 0.0001f * (s1[1] - s0[1]),
                s0[2] - 0.0001f * (s1[2] - s0[2])
            };
        }

        float CalculateLength(float[] p0, float[] p1, float[] p2, float[] p3, float alpha, float step) {
            if (Equals(p1, p2)) {
                return 0f;
            }

            if (Equals(p0, p1)) {
                p0 = Prev(p1, p2);
            }

            if (Equals(p2, p3)) {
                p3 = Prev(p2, p1);
            }

            var t1 = GetT(0f, p0, p1, alpha);
            var t2 = GetT(t1, p1, p2, alpha);
            var t3 = GetT(t2, p2, p3, alpha);

            var baseLength = Length(p2, p1);
            var iterations = (int)(0.9 + Math.Round(baseLength / step));
            var current = p1;
            var result = 0f;

            for (var j = 0; j < iterations; j++) {
                var t = t1 + (t2 - t1) * j / (iterations - 1f);

                var a1 = Sum(Mult((t1 - t) / t1, p0), Mult(t / t1, p1));
                var a2 = Sum(Mult((t2 - t) / (t2 - t1), p1), Mult((t - t1) / (t2 - t1), p2));
                var a3 = Sum(Mult((t3 - t) / (t3 - t2), p2), Mult((t - t2) / (t3 - t2), p3));

                MultSelf((t2 - t) / t2, a1);
                MultSelf((t - t1) / (t3 - t1), a3);

                SumSelf(a1, Mult(t / t2, a2));

                MultSelf((t3 - t) / (t3 - t1), a2);
                SumSelf(a3, a2);

                MultSelf((t2 - t) / (t2 - t1), a1);
                MultSelf((t - t1) / (t2 - t1), a3);
                SumSelf(a1, a3);

                result += Length(a1, current);
                current = a1;
            }

            return result;
        }

        public float CalculateLength() {
            var points = Points;
            var result = 0f;
            for (var j = 0; j < points.Length; j++) {
                var p0 = (j == 0 ? points[points.Length - 1] : points[j - 1]).Position;
                var p1 = points[j].Position;
                if (j == 0 && Length(p0, p1) > 20f) continue;

                result += CalculateLength(j >= 2 ? points[j - 2].Position : p0, p0, p1, j < points.Length - 1 ? points[j + 1].Position : p1,
                        1f, 0.05f);
            }

            return result;
        }

        public Tuple<float, float> CalculateWidth() {
            var points = Points;
            var list = new List<float>(points.Length);
            for (var j = 0; j < points.Length; j++) {
                list.Add(points[j].Width);
            }

            list.Sort();
            return Tuple.Create(list[list.Count / 100], list[list.Count - list.Count / 2 - 1]);
        }
    }
}
