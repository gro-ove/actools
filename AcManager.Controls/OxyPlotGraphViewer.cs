using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using LinearAxis = OxyPlot.Axes.LinearAxis;
using LineSeries = OxyPlot.Series.LineSeries;
using PlotCommands = OxyPlot.PlotCommands;

namespace AcManager.Controls {
    public class CustomTrackerControl : TrackerControl {
        static CustomTrackerControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomTrackerControl), new FrameworkPropertyMetadata(typeof(CustomTrackerControl)));
        }

        public static readonly DependencyProperty PositionOverrideProperty = DependencyProperty.Register(nameof(PositionOverride), typeof(ScreenPoint),
                typeof(CustomTrackerControl), new PropertyMetadata(OnPositionOverrideChanged));

        public ScreenPoint PositionOverride {
            get { return (ScreenPoint)GetValue(PositionOverrideProperty); }
            set { SetValue(PositionOverrideProperty, value); }
        }

        private static void OnPositionOverrideChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomTrackerControl)o).UpdatePositionAndLineExtents();
        }

        public static readonly DependencyProperty LineExtentsOverrideProperty = DependencyProperty.Register(nameof(LineExtentsOverride), typeof(OxyRect),
                typeof(CustomTrackerControl), new PropertyMetadata(OnLineExtentsOverrideChanged));

        public OxyRect LineExtentsOverride {
            get { return (OxyRect)GetValue(LineExtentsOverrideProperty); }
            set { SetValue(LineExtentsOverrideProperty, value); }
        }

        private static void OnLineExtentsOverrideChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomTrackerControl)o).UpdatePositionAndLineExtents();
        }

        private Popup _popup;

        public override void OnApplyTemplate() {
            UpdatePositionAndLineExtents();
            _popup = GetTemplateChild("PART_Popup") as Popup;
            base.OnApplyTemplate();
        }

        private void UpdatePositionAndLineExtents() {
            var position = PositionOverride;
            var extents = LineExtentsOverride;

            var yAxis = (DataContext as TrackerHitResult)?.YAxis;
            Position = position;
            LineExtents = yAxis?.Position == AxisPosition.Right
                    ? new OxyRect(position.X, position.Y, Math.Max(extents.Right - position.X, 0.001), Math.Max(extents.Bottom - position.Y, 0.001))
                    : new OxyRect(extents.Left, position.Y, Math.Max(position.X - extents.Left, 0.001), Math.Max(extents.Bottom - position.Y, 0.001));

            if (yAxis != null) {
                LineStroke = yAxis.AxislineColor.ToBrush();
            }
        }
    }

    public class OxyPlotGraphViewer : PlotView {
        static OxyPlotGraphViewer() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OxyPlotGraphViewer), new FrameworkPropertyMetadata(typeof(OxyPlotGraphViewer)));
        }

        public OxyPlotGraphViewer() {
            Controller = new CustomController();
        }

        private class CustomController : ControllerBase, IPlotController {
            public CustomController() {
                this.BindMouseDown(OxyMouseButton.Left, PlotCommands.PointsOnlyTrack);
                this.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Shift, PlotCommands.Track);
            }
        }

        public static readonly DependencyProperty SourceTorqueProperty =
            DependencyProperty.Register(nameof(SourceTorque), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourceTorqueChanged));

        private static void OnSourceTorqueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdateTorque();
        }

        public static readonly DependencyProperty SourcePowerProperty =
            DependencyProperty.Register(nameof(SourcePower), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourcePowerChanged));

        private static void OnSourcePowerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdatePower();
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(OxyPlotGraphViewer), new PropertyMetadata(OnCarChanged));

        public CarObject Car {
            get { return (CarObject)GetValue(CarProperty); }
            set { SetValue(CarProperty, value); }
        }

        private static void OnCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)o).OnCarChanged((CarObject)e.NewValue);
        }

        private void OnCarChanged(CarObject newValue) {
            if (newValue != null) {
                SourcePower = newValue.SpecsPowerCurve;
                SourceTorque = newValue.SpecsTorqueCurve;
            }
        }

        public static readonly DependencyPropertyKey IsEmptyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEmpty), typeof(bool),
                typeof(OxyPlotGraphViewer), new PropertyMetadata(false));

        public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;

        public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

        [CanBeNull]
        public GraphData SourceTorque {
            get { return (GraphData)GetValue(SourceTorqueProperty); }
            set { SetValue(SourceTorqueProperty, value); }
        }

        [CanBeNull]
        public GraphData SourcePower {
            get { return (GraphData)GetValue(SourcePowerProperty); }
            set { SetValue(SourcePowerProperty, value); }
        }

        private const string KeyRpm = "rpm";
        private const string KeyBhp = "bhp";
        private const string KeyNm = "nm";

        private static readonly OxyColor PowerColor = OxyColor.FromUInt32(0xffff0000);
        private static readonly OxyColor TorqueColor = OxyColor.FromUInt32(0xffffff00);

        private static class CanonicalSplineHelper {
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

        private static class CatmullRomSplineHelper {
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

        private class CatmulLineSeries : LineSeries {
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

        private void CreateModel() {
            Model = new PlotModel {
                TextColor = OxyColor.FromUInt32(0xffffffff),
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = OxyColor.FromUInt32(0x88ffffff),
                LegendPosition = LegendPosition.RightBottom,

                Axes = {
                    new LinearAxis {
                        Key = KeyRpm,
                        Title = ToolsStrings.Units_RPM,
                        TextColor = OxyColors.White,
                        TitleColor = OxyColors.White,
                        TicklineColor = OxyColors.White,
                        AxislineColor = OxyColors.White,
                        Minimum = 0d,
                        Position = AxisPosition.Bottom
                    },
                    new LinearAxis {
                        Key = KeyBhp,
                        Title = ToolsStrings.Units_BHP,
                        TextColor = PowerColor,
                        TitleColor = PowerColor,
                        TicklineColor = PowerColor,
                        AxislineColor = PowerColor,
                        Minimum = 0d,
                        Position = AxisPosition.Right
                    },
                    new LinearAxis {
                        Key = KeyNm,
                        Title = ToolsStrings.Units_Nm,
                        TextColor = TorqueColor,
                        TitleColor = TorqueColor,
                        TicklineColor = TorqueColor,
                        AxislineColor = TorqueColor,
                        Minimum = 0d,
                        Position = AxisPosition.Left
                    }
                },

                Series = {
                    new CatmulLineSeries {
                        Color = PowerColor,
                        Title = ToolsStrings.Common_Power,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyBhp,
                        TrackerKey = KeyBhp,
                        TrackerFormatString = $"[b]{{4:F0}}[/b] {ToolsStrings.Units_BHP} at [b]{{2:F0}}[/b] {ToolsStrings.Units_RPM}"
                    },
                    new CatmulLineSeries {
                        Color = TorqueColor,
                        Title = ToolsStrings.Common_Torque,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyNm,
                        TrackerKey = KeyNm,
                        TrackerFormatString = $"[b]{{4:F0}}[/b] {ToolsStrings.Units_Nm} at [b]{{2:F0}}[/b] {ToolsStrings.Units_RPM}"
                    }
                }
            };

            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(SettingsHolder.Content, nameof(INotifyPropertyChanged.PropertyChanged),
                    ContentSettings_Changed);
        }

        private void ContentSettings_Changed(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.Content.SmoothCurves) || Model == null) return;
            foreach (var series in Model.Series.OfType<LineSeries>()) {
                series.Smooth = SettingsHolder.Content.SmoothCurves;
            }
            InvalidatePlot();
        }

        private void EnsureModelCreated() {
            if (Model == null) {
                CreateModel();
            }
        }

        private void UpdatePower() {
            EnsureModelCreated();
            Model.Replace(KeyBhp, SourcePower);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        private void UpdateTorque() {
            EnsureModelCreated();
            Model.Replace(KeyNm, SourceTorque);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        private static IEnumerable<double> Steps() {
            for (var i = 0; i < 10; i++) {
                var v = Math.Pow(10d, i - 1);
                yield return v;
                yield return v * 2d;
                yield return v * 4d;
                yield return v * 5d;
            }
        }

        private static double GetStep(double maxValue) {
            if (maxValue < 1d || maxValue > 1e6) return double.NaN;

            foreach (var v in Steps()) {
                var a = maxValue / v;
                if (a >= 2d && a < 4d) return v;
            }

            return Math.Pow(10d, Math.Floor(Math.Log10(maxValue)));
        }

        private void UpdateMaximumValues() {
            var power = SourcePower;
            var torque = SourceTorque;

            var maximumValue = Math.Max(power?.MaxY ?? 0d, torque?.MaxY ?? 0d);
            foreach (var axis in Model.Axes.Where(x => x.Position != AxisPosition.Bottom)) {
                axis.Maximum = maximumValue * 1.05;

                var step = GetStep(axis.Maximum);
                axis.MajorStep = step;
                axis.MinorStep = step / 5d;
            }

            SetValue(IsEmptyPropertyKey, !(power?.Points.Count > 1 || torque?.Points.Count > 1));
        }
    }

    internal static class OxyExtenstion {
        public static void Replace(this PlotModel collection, string trackerKey, GraphData data) {
            var series = collection.Series.OfType<LineSeries>().FirstOrDefault(x => x.TrackerKey == trackerKey);
            if (series == null) return;

            series.Points.Clear();
            if (data != null) {
                series.Points.AddRange(data.Points.Select(x => new DataPoint(x.X, x.Y)));
            }
        }
    }
}
