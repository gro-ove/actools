using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Series;

namespace AcManager.Controls.Graphs {
    public class PieSeriesExt : PieSeries {
        public override void Render(IRenderContext rc, PlotModel model) {
            if (Slices.Count == 0) {
                return;
            }

            var total = Slices.Sum(slice => slice.Value);
            if (Math.Abs(total) < double.Epsilon) {
                return;
            }

            var radius = Math.Min(model.PlotArea.Width, model.PlotArea.Height) / 2;
            var outerRadius = radius * (Diameter - ExplodedDistance);
            var innerRadius = radius * InnerDiameter;

            var angle = StartAngle;
            var midPoint = new ScreenPoint((model.PlotArea.Left + model.PlotArea.Right) * 0.5, (model.PlotArea.Top + model.PlotArea.Bottom) * 0.5);
            foreach (var slice in Slices) {
                var outerPoints = new List<ScreenPoint>();
                var innerPoints = new List<ScreenPoint>();

                var sliceAngle = slice.Value / total * AngleSpan;
                var endAngle = angle + sliceAngle;
                var explodedRadius = slice.IsExploded ? ExplodedDistance * radius : 0.0;

                var midAngle = angle + sliceAngle / 2;
                var midAngleRadians = midAngle * Math.PI / 180;
                var mp = new ScreenPoint(
                        midPoint.X + explodedRadius * Math.Cos(midAngleRadians),
                        midPoint.Y + explodedRadius * Math.Sin(midAngleRadians));

                // Create the pie sector points for both outside and inside arcs
                while (true) {
                    var stop = false;
                    if (angle >= endAngle) {
                        angle = endAngle;
                        stop = true;
                    }

                    var a = angle * Math.PI / 180;
                    var op = new ScreenPoint(mp.X + outerRadius * Math.Cos(a), mp.Y + outerRadius * Math.Sin(a));
                    outerPoints.Add(op);
                    var ip = new ScreenPoint(mp.X + innerRadius * Math.Cos(a), mp.Y + innerRadius * Math.Sin(a));
                    if (innerRadius + explodedRadius > 0) {
                        innerPoints.Add(ip);
                    }

                    if (stop) {
                        break;
                    }

                    angle += AngleIncrement;
                }

                innerPoints.Reverse();
                if (innerPoints.Count == 0) {
                    innerPoints.Add(mp);
                }

                innerPoints.Add(outerPoints[0]);

                var points = outerPoints;
                points.AddRange(innerPoints);

                rc.DrawPolygon(points, slice.ActualFillColor, Stroke, StrokeThickness, null, LineJoin.Bevel);
            }

            angle = StartAngle;
            foreach (var slice in Slices) {
                var sliceAngle = slice.Value / total * AngleSpan;
                var explodedRadius = slice.IsExploded ? ExplodedDistance * radius : 0.0;

                var midAngle = angle + sliceAngle / 2;
                var midAngleRadians = midAngle * Math.PI / 180;
                var mp = new ScreenPoint(
                        midPoint.X + explodedRadius * Math.Cos(midAngleRadians),
                        midPoint.Y + explodedRadius * Math.Sin(midAngleRadians));
                angle = angle + sliceAngle;

                // Render label outside the slice
                if (OutsideLabelFormat != null) {
                    var label = string.Format(
                            OutsideLabelFormat, slice.Value, slice.Label, slice.Value / total * 100);
                    var sign = Math.Sign(Math.Cos(midAngleRadians));

                    // tick points
                    var tp0 = new ScreenPoint(
                            mp.X + (outerRadius + TickDistance) * Math.Cos(midAngleRadians),
                            mp.Y + (outerRadius + TickDistance) * Math.Sin(midAngleRadians));
                    var tp1 = new ScreenPoint(
                            tp0.X + TickRadialLength * Math.Cos(midAngleRadians),
                            tp0.Y + TickRadialLength * Math.Sin(midAngleRadians));
                    var tp2 = new ScreenPoint(tp1.X + TickHorizontalLength * sign, tp1.Y);
                    rc.DrawLine(new[] { tp0, tp1, tp2 }, Stroke, 1d, null, LineJoin.Bevel);

                    // label
                    var labelPosition = new ScreenPoint(tp2.X + TickLabelDistance * sign, tp2.Y);
                    DrawTextWithShadow(rc, labelPosition, label, ActualTextColor, ActualFont, ActualFontSize, ActualFontWeight,
                            0, sign > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Middle);
                }

                // Render label inside the slice
                if (InsideLabelFormat != null) {
                    var label = string.Format(
                            InsideLabelFormat, slice.Value, slice.Label, slice.Value / total * 100);
                    var r = innerRadius * (1 - InsideLabelPosition) + outerRadius * InsideLabelPosition;
                    var labelPosition = new ScreenPoint(
                            mp.X + r * Math.Cos(midAngleRadians), mp.Y + r * Math.Sin(midAngleRadians));
                    double textAngle = 0;
                    if (AreInsideLabelsAngled) {
                        textAngle = midAngle;
                        if (Math.Cos(midAngleRadians) < 0) {
                            textAngle += 180;
                        }
                    }

                    DrawTextWithShadow(rc, labelPosition, label, ActualTextColor, ActualFont, ActualFontSize, ActualFontWeight,
                            textAngle, HorizontalAlignment.Center, VerticalAlignment.Middle);
                }
            }

            void DrawTextWithShadow(IRenderContext context, ScreenPoint p, string text, OxyColor fill, string fontFamily = null, double fontSize = 10.0, double fontWeight = 400.0,
                    double rotation = 0.0, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment verticalAlignment = VerticalAlignment.Top, OxySize? maxSize = null) {
                for (var i = -1; i <= 1; ++i) {
                    for (var j = -1; j <= 1; ++j) {
                        if (Math.Abs(i) + Math.Abs(j) != 1) continue;
                        context.DrawText(p + new ScreenVector(i, j), text, OxyColors.Black, fontFamily, fontSize, fontWeight,
                                rotation, horizontalAlignment, verticalAlignment, maxSize);
                    }
                }
                context.DrawText(p, text, fill, fontFamily, fontSize, fontWeight, rotation, horizontalAlignment, verticalAlignment, maxSize);
            }
        }
    }
}