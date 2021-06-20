using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Data;
using JetBrains.Annotations;
using OxyPlot;
using OxyPlot.Wpf;
using LineSeries = OxyPlot.Series.LineSeries;

namespace AcManager.Controls.Graphs {
    public static class OxyExtenstion {
        public static OxyColor ToOxyColor([NotNull] this FrameworkElement element, [NotNull] string key) {
            var r = element.TryFindResource(key);
            var v = r as Color? ?? (r as SolidColorBrush)?.Color ?? Colors.Magenta;
            return v.ToOxyColor();
        }

        public static void Replace([NotNull] this PlotModel collection, [NotNull] string trackerKey, [CanBeNull] GraphData data) {
            collection.Replace(trackerKey, data?.Points.Select(x => new DataPoint(x.X, x.Y)));
        }

        public static void Replace([NotNull] this PlotModel collection, [NotNull] string trackerKey, [CanBeNull] IEnumerable<DataPoint> points) {
            var series = collection.Series.OfType<LineSeries>().FirstOrDefault(x => x.TrackerKey == trackerKey);
            if (series == null) return;

            series.Points.Clear();
            if (points != null) {
                series.Points.AddRange(points);
            }
        }

        public static double MaxY([NotNull] this IEnumerable<DataPoint> points) {
            return points.Select(x => x.Y).Max();
        }
    }
}