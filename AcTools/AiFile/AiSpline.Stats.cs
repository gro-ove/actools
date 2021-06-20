using System;
using System.Collections.Generic;
using AcTools.Numerics;
using AcTools.Utils.Helpers;

namespace AcTools.AiFile {
    public partial class AiSpline {
        private static float Sqr(float v) {
            return v * v;
        }

        // TODO: VEC3
        private static float LengthSqr(Vec3 a, Vec3 b) {
            return Sqr(a[0] - b[0]) + Sqr(a[1] - b[1]) + Sqr(a[2] - b[2]);
        }

        private static float Length(Vec3 a, Vec3 b) {
            return (float)Math.Sqrt(LengthSqr(a, b));
        }

        private static float GetT(double t, Vec3 p0, Vec3 p1, double alpha) {
            return (float)(Math.Pow(Math.Pow(LengthSqr(p1, p0), 0.5), alpha) + t);
        }

        private static Vec3 Mult(float s, Vec3 d) {
            return new Vec3(d[0] * s, d[1] * s, d[2] * s);
        }

        private static void MultSelf(float s, ref Vec3 d) {
            d[0] *= s;
            d[1] *= s;
            d[2] *= s;
        }

        private static Vec3 Sum(Vec3 a, Vec3 b) {
            return new Vec3(a[0] + b[0], a[1] + b[1], a[2] + b[2]);
        }

        private static void SumSelf(ref Vec3 a, Vec3 b) {
            a[0] += b[0];
            a[1] += b[1];
            a[2] += b[2];
        }

        private static bool Equals(Vec3 a, Vec3 b) {
            return Equals(a[0], b[0]) && Equals(a[1], b[1]) && Equals(a[2], b[2]);
        }

        private static Vec3 Prev(Vec3 s0, Vec3 s1) {
            return new Vec3(
                    s0[0] - 0.0001f * (s1[0] - s0[0]),
                    s0[1] - 0.0001f * (s1[1] - s0[1]),
                    s0[2] - 0.0001f * (s1[2] - s0[2]));
        }

        float CalculateLength(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, float alpha, float step) {
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

                MultSelf((t2 - t) / t2, ref a1);
                MultSelf((t - t1) / (t3 - t1), ref a3);

                SumSelf(ref a1, Mult(t / t2, a2));

                MultSelf((t3 - t) / (t3 - t1), ref a2);
                SumSelf(ref a3, a2);

                MultSelf((t2 - t) / (t2 - t1), ref a1);
                MultSelf((t - t1) / (t2 - t1), ref a3);
                SumSelf(ref a1, a3);

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
            var points = PointsExtra;
            var list = new List<float>(points.Length);
            for (var j = 0; j < points.Length; j++) {
                list.AddSorted(points[j].Width);
            }

            return Tuple.Create(list[list.Count / 100], list[list.Count - list.Count / 2 - 1]);
        }
    }
}
