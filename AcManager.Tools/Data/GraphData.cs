using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class GraphData {
        private readonly Dictionary<double, double> _values;
        public IReadOnlyDictionary<double, double> Values => _values;

        private readonly List<Point> _normalizedValuesArray;
        public IReadOnlyList<Point> NormalizedValuesArray => _normalizedValuesArray;

        public readonly double MinX, MaxX, MinY, MaxY;

        public GraphData(Dictionary<double, double> values) {
            _values = values;

            if (Values.Count == 0) {
                MinX = MaxX = MinY = MaxY = 0.0;
                _normalizedValuesArray = new List<Point>();
                return;
            }

            MinX = Values.Keys.Min();
            MaxX = Values.Keys.Max();

            MinY = Values.Values.Min();
            MaxY = Values.Values.Max();

            _normalizedValuesArray =
                Values.Select(x => new Point((x.Key - MinX) / (MaxX - MinX), (x.Value - MinY) / (MaxY - MinY))).ToList();
        }

        public GraphData(JArray obj = null) : this(ConvertToValues(obj)) {
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static Dictionary<double, double> ConvertToValues(JArray obj) {
            var values = new Dictionary<double, double>();
            if (obj == null) return values;

            foreach (var entry in obj.OfType<JArray>()) {
                double key, value;
                if (double.TryParse(Convert.ToString(entry[0]), out key) &&
                    double.TryParse(Convert.ToString(entry[1]), out value)) {
                    values[key] = value;
                }
            }

            return values;
        }

        public GraphData ScaleTo(double maxY) {
            var multipler = Math.Abs(MaxY) < 0.001 ? 1.0 : maxY/MaxY;
            return ScaleBy(multipler);
        }

        public GraphData ScaleBy(double multipler) {
            return new GraphData(Values.ToDictionary(x => x.Key, x => x.Value * multipler));
        }

        public JArray ToJArray() {
            return new JArray(Values.Select(x => new JArray(x.Key, x.Value)).ToArray<object>());
        }
    }
}
