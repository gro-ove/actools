// Source: http://theinstructionlimit.com/fast-uniform-poisson-disk-sampling-in-c by Renaud Bédard

// Adapated from java source by Herman Tulleken
// http://www.luma.co.za/labs/2008/02/27/poisson-disk-sampling/

// The algorithm is from the "Fast Poisson Disk Sampling in Arbitrary Dimensions" paper by Robert Bridson
// http://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf

using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Base.Utils {
    public static class UniformPoissonDiskSampler {
        #region Main public methods
        public const int DefaultPointsPerIteration = 30;

        public static List<Vector2> SampleCircle(Vector2 center, float radius, float minimumDistance) {
            return SampleCircle(center, radius, minimumDistance, DefaultPointsPerIteration);
        }

        public static List<Vector2> SampleCircle(Vector2 center, float radius, float minimumDistance, int pointsPerIteration) {
            return Sample(center - new Vector2(radius), center + new Vector2(radius), radius, minimumDistance, pointsPerIteration);
        }

        public static List<Vector2> SampleRectangle(Vector2 topLeft, Vector2 lowerRight, float minimumDistance) {
            return SampleRectangle(topLeft, lowerRight, minimumDistance, DefaultPointsPerIteration);
        }

        public static List<Vector2> SampleRectangle(Vector2 topLeft, Vector2 lowerRight, float minimumDistance, int pointsPerIteration) {
            return Sample(topLeft, lowerRight, null, minimumDistance, pointsPerIteration);
        }
        #endregion

        #region Some methods for AcTools.Render
        /// <summary>
        /// Estimate minimumDistance value for disk with unit radius to get a desired amount of samples.
        /// </summary>
        public static float EstimateCircleMinimumDistance(int count){
            var lc = Math.Log10(count);
            var li = lc * 0.511 - 0.184;
            return (float)Math.Pow(10, -li);
        }

        /// <summary>
        /// Estimate minimumDistance value for a square with unit size to get a desired amount of samples.
        /// </summary>
        public static float EstimateSquareMinimumDistance(int count){
            var lc = Math.Log10(count);
            var li = lc * 0.51 - 0.235;
            return (float)Math.Pow(10, -li);
        }

        /// <summary>
        /// Get a number of vectors uniformly distibuted within a unit-radius circle. Size of result list might be a little smaller.
        /// </summary>
        public static List<Vector2> SampleCircle(int count) {
            if (count <= 1) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var minimumDistance = EstimateCircleMinimumDistance(count + 10);

            var result = SampleCircle(Vector2.Zero, 1f, minimumDistance);
            while (result.Count > count) {
                result.Remove(result.MaxEntry(x => x.LengthSquared()));
            }

            var maximumLength = (float)Math.Sqrt(result.Max(x => x.LengthSquared()));
            for (var i = 0; i < result.Count; i++) {
                result[i] = result[i] / maximumLength;
            }

            return result;
        }

        /// <summary>
        /// Get a number of vectors uniformly distibuted within a unit-size square. Size of result list might be a little smaller.
        /// </summary>
        public static List<Vector2> SampleSquare(int count) {
            if (count <= 1) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var minimumDistance = EstimateCircleMinimumDistance(count + 10);

            var result = SampleRectangle(new Vector2(-1f, -1f), new Vector2(1f, 1f), minimumDistance);
            while (result.Count > count) {
                result.Remove(result.MaxEntry(x => Math.Max(x.X, x.Y)));
            }

            var maximumLength = result.Max(x => Math.Max(x.X, x.Y));
            for (var i = 0; i < result.Count; i++) {
                result[i] = result[i] / maximumLength;
            }

            return result;
        }
        #endregion

        #region Internal structs
        private static readonly float SquareRootTwo = (float)Math.Sqrt(2);
        private static readonly float TwoPi = (float)(Math.PI * 2d);

        private struct Settings {
            public Vector2 TopLeft, LowerRight, Center;
            public Vector2 Dimensions;
            public float? RejectionSqDistance;
            public float MinimumDistance;
            public float CellSize;
            public int GridWidth, GridHeight;
        }

        private struct State {
            public Vector2?[,] Grid;
            public List<Vector2> ActivePoints, Points;
        }
        #endregion

        #region Private methods
        private static List<Vector2> Sample(Vector2 topLeft, Vector2 lowerRight, float? rejectionDistance, float minimumDistance, int pointsPerIteration) {
            var settings = new Settings {
                TopLeft = topLeft,
                LowerRight = lowerRight,
                Dimensions = lowerRight - topLeft,
                Center = (topLeft + lowerRight) / 2,
                CellSize = minimumDistance / SquareRootTwo,
                MinimumDistance = minimumDistance,
                RejectionSqDistance = rejectionDistance * rejectionDistance
            };

            settings.GridWidth = (int)(settings.Dimensions.X / settings.CellSize) + 1;
            settings.GridHeight = (int)(settings.Dimensions.Y / settings.CellSize) + 1;

            var state = new State {
                Grid = new Vector2?[settings.GridWidth, settings.GridHeight],
                ActivePoints = new List<Vector2>(),
                Points = new List<Vector2>()
            };

            AddFirstPoint(ref settings, ref state);

            while (state.ActivePoints.Count != 0) {
                var listIndex = MathUtils.Random(state.ActivePoints.Count);
                var point = state.ActivePoints[listIndex];
                var found = false;

                for (var k = 0; k < pointsPerIteration; k++) {
                    found |= AddNextPoint(point, ref settings, ref state);
                }

                if (!found) {
                    state.ActivePoints.RemoveAt(listIndex);
                }
            }

            return state.Points;
        }

        private static void AddFirstPoint(ref Settings settings, ref State state) {
            var added = false;
            while (!added) {
                var p = new Vector2(
                        settings.TopLeft.X + settings.Dimensions.X * MathF.Random(),
                        settings.TopLeft.Y + settings.Dimensions.Y * MathF.Random());
                if (settings.RejectionSqDistance != null && Vector2.DistanceSquared(settings.Center, p) > settings.RejectionSqDistance) {
                    continue;
                }

                var index = Denormalize(p, settings.TopLeft, settings.CellSize);
                state.Grid[(int)index.X, (int)index.Y] = p;
                state.ActivePoints.Add(p);
                state.Points.Add(p);
                added = true;
            }
        }

        private static bool AddNextPoint(Vector2 point, ref Settings settings, ref State state) {
            var q = GenerateRandomAround(point, settings.MinimumDistance);
            if (q.X < settings.TopLeft.X || q.X > settings.LowerRight.X || q.Y < settings.TopLeft.Y || q.Y > settings.LowerRight.Y
                    || settings.RejectionSqDistance != null && Vector2.DistanceSquared(settings.Center, q) > settings.RejectionSqDistance) {
                return false;
            }

            var qIndex = Denormalize(q, settings.TopLeft, settings.CellSize);

            var iLimit = Math.Min(settings.GridWidth, qIndex.X + 3);
            var jLimit = Math.Min(settings.GridHeight, qIndex.Y + 3);
            for (var i = (int)Math.Max(0, qIndex.X - 2); i < iLimit; i++) {
                for (var j = (int)Math.Max(0, qIndex.Y - 2); j < jLimit; j++) {
                    if (state.Grid[i, j].HasValue && Vector2.Distance(state.Grid[i, j].Value, q) < settings.MinimumDistance) {
                        return false;
                    }
                }
            }

            state.ActivePoints.Add(q);
            state.Points.Add(q);
            state.Grid[(int)qIndex.X, (int)qIndex.Y] = q;
            return true;
        }

        private static Vector2 GenerateRandomAround(Vector2 center, float minimumDistance) {
            var radius = minimumDistance + minimumDistance * MathF.Random();
            var angle = TwoPi * MathF.Random();
            return new Vector2(center.X + radius * angle.Sin(), center.Y + radius * angle.Cos());
        }

        private static Vector2 Denormalize(Vector2 point, Vector2 origin, double cellSize) {
            return new Vector2((int)((point.X - origin.X) / cellSize), (int)((point.Y - origin.Y) / cellSize));
        }
        #endregion
    }
}