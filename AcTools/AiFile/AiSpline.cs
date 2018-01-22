using System;
using System.IO;
using ImageMagick;
using JetBrains.Annotations;

namespace AcTools.AiFile {
    public partial class AiSpline {
        public readonly int Version;

        // Extra values, usually zeroes
        public readonly int LapTime;
        public readonly int SampleCount;

        public readonly AiPoint[] Points;
        public readonly AiPointExtra[] PointsExtra;
        public bool HasGrid;
        public readonly AiSplineGrid Grid;

        private AiSpline([NotNull] string filename) {
            using (var reader = new ReadAheadBinaryReader(filename)) {
                Version = reader.ReadInt32();
                if (Version != 7) throw new Exception($"Version {Version} not supported");

                var points = new AiPoint[reader.ReadInt32()];
                LapTime = reader.ReadInt32();
                SampleCount = reader.ReadInt32();

                for (int i = 0, c = points.Length; i < c; i++) {
                    points[i].LoadFrom(reader);
                }

                var pointsExtra = new AiPointExtra[reader.ReadInt32()];
                for (int i = 0, c = pointsExtra.Length; i < c; i++) {
                    pointsExtra[i].LoadFrom(reader);
                }

                Points = points;
                PointsExtra = pointsExtra;

                HasGrid = reader.ReadInt32() != 0;
                if (HasGrid) {
                    Grid = new AiSplineGrid(reader);
                }
            }
        }

        public static AiSpline FromFile([NotNull] string filename) {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            return new AiSpline(filename);
        }
    }
}
