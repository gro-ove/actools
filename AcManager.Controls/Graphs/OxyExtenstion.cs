using System.Linq;
using AcManager.Tools.Data;
using OxyPlot;
using OxyPlot.Series;

namespace AcManager.Controls.Graphs {
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