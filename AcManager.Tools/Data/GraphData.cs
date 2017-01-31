using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Data {
    public class GraphData {
        [NotNull]
        private readonly Lut _points;

        [NotNull]
        public IReadOnlyList<LutPoint> Points => _points;

        private IReadOnlyList<Point> _normalizedValuesArray;

        public IReadOnlyList<Point> NormalizedValuesArray => _normalizedValuesArray ?? (_normalizedValuesArray = GetNormalizedValuesArray());

        private IReadOnlyList<Point> GetNormalizedValuesArray() {
            var result = new Point[_points.Count];
            for (var i = 0; i < result.Length; i++) {
                var x = _points[i];
                result[i] = new Point((x.X - MinX) / (MaxX - MinX), (x.Y - MinY) / (MaxY - MinY));
            }
            return result;
        }

        public readonly double MinX, MaxX, MinY, MaxY;

        public GraphData([NotNull] Lut points) {
            _points = points;

            if (Points.Count == 0) {
                MinX = MaxX = MinY = MaxY = 0.0;
                _normalizedValuesArray = new List<Point>();
                return;
            }

            _points.UpdateBoundingBox();
            MinX = _points.MinX;
            MaxX = _points.MaxX;
            MinY = _points.MinY;
            MaxY = _points.MaxY;
        }

        public GraphData(JArray obj = null) : this(ConvertToValues(obj)) {}

        private static Lut ConvertToValues(JArray obj) {
            var values = new Lut();
            if (obj == null) return values;

            foreach (var entry in obj.OfType<JArray>().Where(x => x.Count == 2)) {
                double x, y;
                if (FlexibleParser.TryParseDouble(Convert.ToString(entry[0], CultureInfo.InvariantCulture), out x) &&
                        FlexibleParser.TryParseDouble(Convert.ToString(entry[1], CultureInfo.InvariantCulture), out y)) {
                    values.Add(new LutPoint(x, y));
                }
            }

            return values;
        }

        public JArray ToJArray() {
            return new JArray(Points.Select(x => new JArray(x.X, x.Y)).ToArray<object>());
        }

        public Lut ToLut() {
            return new Lut(_points);
        }
    }
}
