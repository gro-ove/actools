using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;

namespace AcManager.Controls.Graphs {
    internal static class CatmullRomSplineHelper {
        internal static List<DataPoint> CreateSpline(List<DataPoint> points, double alpha, double tolerance) {
            var spline = CreateSpline(points.Select(p => new ScreenPoint(p.X, p.Y)).ToList(), alpha, tolerance);
            var dataPointList = new List<DataPoint>(spline.Count);
            dataPointList.AddRange(spline.Select(screenPoint => new DataPoint(screenPoint.X, screenPoint.Y)));
            return dataPointList;
        }

        internal static List<ScreenPoint> CreateSpline(IList<ScreenPoint> points, double alpha, double tolerance) {
            var screenPointList = new List<ScreenPoint>();
            if (points == null) return screenPointList;

            var count = points.Count;
            if (count < 2) {
                screenPointList.AddRange(points);
                return screenPointList;
            }

            for (var i = 0; i < count - 1; ++i) {
                Segment(screenPointList, points[i == 0 ? 0 : i - 1], points[i], points[i + 1], points[i == count - 2 ? i + 1 : i + 2], alpha,
                        tolerance);
            }

            return screenPointList;
        }

        private static void Segment(IList<ScreenPoint> points, ScreenPoint p0, ScreenPoint p1, ScreenPoint p2, ScreenPoint p3, double alpha,
                double tolerance) {
            if (Equals(p1, p2)) {
                points.Add(p1);
                return;
            }

            if (Equals(p0, p1)) {
                p0 = Prev(p1, p2);
            }

            if (Equals(p2, p3)) {
                p3 = Prev(p2, p1);
            }

            var t0 = 0d;
            var t1 = GetT(t0, p0, p1, alpha);
            var t2 = GetT(t1, p1, p2, alpha);
            var t3 = GetT(t2, p2, p3, alpha);

            var iterations = (int)((Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y)) / tolerance);
            for (var t = t1; t < t2; t += (t2 - t1) / iterations) {
                var a1 = Sum(Mult((t1 - t) / (t1 - t0), p0), Mult((t - t0) / (t1 - t0), p1));
                var a2 = Sum(Mult((t2 - t) / (t2 - t1), p1), Mult((t - t1) / (t2 - t1), p2));
                var a3 = Sum(Mult((t3 - t) / (t3 - t2), p2), Mult((t - t2) / (t3 - t2), p3));

                var b1 = Sum(Mult((t2 - t) / (t2 - t0), a1), Mult((t - t0) / (t2 - t0), a2));
                var b2 = Sum(Mult((t3 - t) / (t3 - t1), a2), Mult((t - t1) / (t3 - t1), a3));

                var c1 = Sum(Mult((t2 - t) / (t2 - t1), b1), Mult((t - t1) / (t2 - t1), b2));
                points.Add(c1);
            }
        }

        private static double GetT(double t, ScreenPoint p0, ScreenPoint p1, double alpha) {
            var a = Math.Pow(p1.X - p0.X, 2d) + Math.Pow(p1.Y - p0.Y, 2d);
            var b = Math.Pow(a, 0.5);
            var c = Math.Pow(b, alpha);
            return c + t;
        }

        private static ScreenPoint Mult(double d, ScreenPoint s) {
            return new ScreenPoint(s.X * d, s.Y * d);
        }

        private static bool Equals(ScreenPoint a, ScreenPoint b) {
            return Equals(a.X, b.X) && Equals(a.Y, b.Y);
        }

        private static ScreenPoint Prev(ScreenPoint s0, ScreenPoint s1) {
            return new ScreenPoint(s0.X - 0.0001 * (s1.X - s0.X), s0.Y - 0.0001 * (s1.Y - s0.Y));
        }

        private static ScreenPoint Sum(ScreenPoint a, ScreenPoint b) {
            return new ScreenPoint(a.X + b.X, a.Y + b.Y);
        }
    }
}