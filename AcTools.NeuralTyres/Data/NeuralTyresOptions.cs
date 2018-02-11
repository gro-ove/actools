using System;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcTools.NeuralTyres.Data {
    public class NeuralTyresOptions {
        public static readonly NeuralTyresOptions Default = new NeuralTyresOptions();

        public static readonly string InputWidth = "WIDTH";
        public static readonly string InputRadius = "RADIUS";
        public static readonly string InputRimRadius = "RIM_RADIUS";
        public static readonly string InputProfile = "PROFILE";

        [NotNull]
        public string[] InputKeys = { InputWidth, InputRadius, InputProfile };

        [NotNull]
        public string[] IgnoredKeys = {
            InputWidth, InputRadius, InputRimRadius, InputProfile,
            "NAME", "SHORT_NAME", "WEAR_CURVE", "THERMAL@PERFORMANCE_CURVE",
            "DY0", "DY1", "DX0", "DX1"
        };

        [NotNull]
        public int[] Layers = { 15, 15 };

        public int TrainingRuns = 500000;
        public bool SeparateNetworks = true;
        public double LearningRate = 0.2;
        public double LearningMomentum = 0.1;
        public double ValuePadding = 0.3;
        public double RandomBounds = 1d;
        public bool HighPrecision = false;
        public int AverageAmount = 3;
        public bool TrainAverageInParallel = false;
        public FannTrainingAlgorithm FannAlgorithm = FannTrainingAlgorithm.Incremental;

        [CanBeNull]
        public string[] OverrideOutputKeys;

        // Envinroment setting, doesn’t affect hash code or equality
        [JsonIgnore]
        public int MaxDegreeOfParallelism = (Environment.ProcessorCount / 2).Clamp(1, 4);

        protected bool Equals(NeuralTyresOptions other) {
            return Layers.SequenceEqual(other.Layers)
                    && SeparateNetworks.Equals(other.SeparateNetworks) && LearningRate.Equals(other.LearningRate)
                    && LearningMomentum.Equals(other.LearningMomentum) && ValuePadding.Equals(other.ValuePadding)
                    && HighPrecision == other.HighPrecision && AverageAmount == other.AverageAmount
                    && InputKeys.SequenceEqual(other.InputKeys) && IgnoredKeys.SequenceEqual(other.IgnoredKeys)
                    && (OverrideOutputKeys == null
                            ? other.OverrideOutputKeys == null
                            : other.OverrideOutputKeys != null && OverrideOutputKeys.SequenceEqual(other.OverrideOutputKeys))
                    && TrainAverageInParallel == other.TrainAverageInParallel;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((NeuralTyresOptions)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ InputKeys.GetEnumerableHashCode();
                hashCode = (hashCode * 397) ^ IgnoredKeys.GetEnumerableHashCode();
                hashCode = (hashCode * 397) ^ OverrideOutputKeys.GetEnumerableHashCode();
                hashCode = (hashCode * 397) ^ Layers.GetEnumerableHashCode();
                hashCode = (hashCode * 397) ^ SeparateNetworks.GetHashCode();
                hashCode = (hashCode * 397) ^ LearningRate.GetHashCode();
                hashCode = (hashCode * 397) ^ LearningMomentum.GetHashCode();
                hashCode = (hashCode * 397) ^ ValuePadding.GetHashCode();
                hashCode = (hashCode * 397) ^ HighPrecision.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageAmount.GetHashCode();
                hashCode = (hashCode * 397) ^ TrainAverageInParallel.GetHashCode();
                return hashCode;
            }
        }
    }
}