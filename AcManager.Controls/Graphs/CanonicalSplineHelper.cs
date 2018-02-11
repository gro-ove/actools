using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;

namespace AcManager.Controls.Graphs {
    internal static class CanonicalSplineHelper {
        internal static List<DataPoint> CreateSpline(List<DataPoint> points, double tension, double tolerance) {
            var spline = CreateSpline(points.Select(p => new ScreenPoint(p.X, p.Y)).ToList(), tension, tolerance);
            var dataPointList = new List<DataPoint>(spline.Count);
            dataPointList.AddRange(spline.Select(screenPoint => new DataPoint(screenPoint.X, screenPoint.Y)));
            return dataPointList;
        }

        internal static List<ScreenPoint> CreateSpline(IList<ScreenPoint> points, double tension, double tolerance) {
            var screenPointList = new List<ScreenPoint>(points?.Count ?? 0);
            if (points == null) return screenPointList;

            var count = points.Count;
            if (count < 2) {
                screenPointList.AddRange(points);
                return screenPointList;
            }

            for (var i = 0; i < count - 1; ++i) {
                Segment(screenPointList, points[i == 0 ? 0 : i - 1], points[i], points[i + 1], points[i == count - 2 ? i + 1 : i + 2], tension,
                        tolerance);
            }

            return screenPointList;
        }

        private static void Segment(IList<ScreenPoint> points, ScreenPoint pt0, ScreenPoint pt1, ScreenPoint pt2, ScreenPoint pt3, double tension,
                double tolerance) {
            var num1 = tension * (pt2.X - pt0.X);
            var num2 = tension * (pt2.Y - pt0.Y);
            var num3 = tension * (pt3.X - pt1.X);
            var num4 = tension * (pt3.Y - pt1.Y);
            var num5 = num1 + num3 + 2.0 * pt1.X - 2.0 * pt2.X;
            var num6 = num2 + num4 + 2.0 * pt1.Y - 2.0 * pt2.Y;
            var num7 = -2.0 * num1 - num3 - 3.0 * pt1.X + 3.0 * pt2.X;
            var num8 = -2.0 * num2 - num4 - 3.0 * pt1.Y + 3.0 * pt2.Y;
            var num9 = num1;
            var num10 = num2;
            var x = pt1.X;
            var y = pt1.Y;
            var num11 = (int)((Math.Abs(pt1.X - pt2.X) + Math.Abs(pt1.Y - pt2.Y)) / tolerance);
            for (var index = 1; index < num11; ++index) {
                var num12 = index / (double)(num11 - 1);
                var screenPoint = new ScreenPoint(num5 * num12 * num12 * num12 + num7 * num12 * num12 + num9 * num12 + x,
                        num6 * num12 * num12 * num12 + num8 * num12 * num12 + num10 * num12 + y);
                points.Add(screenPoint);
            }
        }
    }
}