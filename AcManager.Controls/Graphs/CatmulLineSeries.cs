using System;
using System.Collections.Generic;
using AcManager.Tools.Helpers;
using OxyPlot;
using OxyPlot.Series;

namespace AcManager.Controls.Graphs {
    public class CatmulLineSeries : LineSeries {
        private readonly double _smoothessMultipler;

        public CatmulLineSeries(double smoothessMultipler = 0.1) {
            _smoothessMultipler = smoothessMultipler;

            // TrackerFormatString = FormatString;
            Smooth = SettingsHolder.Content.SmoothCurves;
            CanTrackerInterpolatePoints = SettingsHolder.Content.SmoothCurves;
        }

        protected override void RenderLineAndMarkers(IRenderContext rc, OxyRect clippingRect, IList<ScreenPoint> pointsToRender) {
            if (Smooth) {
                pointsToRender = CatmullRomSplineHelper.CreateSpline(ScreenPointHelper.ResamplePoints(pointsToRender, MinimumSegmentLength), 0.5,
                        0.25 / _smoothessMultipler);
            }

            if (StrokeThickness > 0.0 && ActualLineStyle != LineStyle.None) {
                RenderLine(rc, clippingRect, pointsToRender);
            }

            if (MarkerType != MarkerType.None) {
                var binOffset = MarkerResolution > 0 ? Transform(MinX, MinY) : new ScreenPoint();
                rc.DrawMarkers(clippingRect, pointsToRender, MarkerType, MarkerOutline, new[] { MarkerSize }, ActualMarkerFill, MarkerStroke,
                        MarkerStrokeThickness, MarkerResolution, binOffset);
            }
        }

        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate) {
            return Points.Count < 2 ? null : base.GetNearestPoint(point, interpolate);
        }

        protected override HitTestResult HitTestOverride(HitTestArguments args) {
            return Points.Count < 2 ? null : base.HitTestOverride(args);
        }

        protected override void ResetSmoothedPoints() {
            SmoothedPoints = CatmullRomSplineHelper.CreateSpline(ActualPoints, 0.5, Math.Abs(Math.Max(MaxX - MinX, MaxY - MinY) / 200d));
        }
    }
}