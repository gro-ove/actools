using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using OxyPlot;
using LineSeries = OxyPlot.Series.LineSeries;

namespace AcManager.Controls.Graphs {
    public static class OxyExtenstion {
        public static void Replace([NotNull] this PlotModel collection, [NotNull] string trackerKey, [CanBeNull] GraphData data) {
            collection.Replace(trackerKey, data?.Points.Select(x => new DataPoint(x.X, x.Y)));
        }

        public static void Replace([NotNull] this PlotModel collection, [NotNull] string trackerKey, [CanBeNull] IEnumerable<DataPoint> points) {
            var series = collection.Series.OfType<LineSeries>().FirstOrDefault(x => x.TrackerKey == trackerKey);
            Logging.Warning(series);
            if (series == null) return;

            series.Points.Clear();
            if (points != null) {
                Logging.Here();
                series.Points.AddRange(points);
            }
        }

        public static double MaxY([NotNull] this IEnumerable<DataPoint> points) {
            return points.Select(x => x.Y).Max();
        }
    }
}