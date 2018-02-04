using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

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
            "NAME", "SHORT_NAME", "WEAR_CURVE", "THERMAL@PERFORMANCE_CURVE"
        };

        [NotNull]
        public int[] NetworkLayers = { 15, 15 };

        public double LearningRate = 0.2;
        public double LearningMomentum = 0.1;
        public double ValuePadding = 0.3;
        public bool AccurateCalculations = true;
        public int AverageAmount = 3;
        public bool TrainAverageInParallel = false;

        protected bool Equals(NeuralTyresOptions other) {
            return NetworkLayers.SequenceEqual(other.NetworkLayers) && LearningRate.Equals(other.LearningRate)
                    && LearningMomentum.Equals(other.LearningMomentum) && ValuePadding.Equals(other.ValuePadding)
                    && AccurateCalculations == other.AccurateCalculations && AverageAmount == other.AverageAmount
                    && InputKeys.SequenceEqual(other.InputKeys) && IgnoredKeys.SequenceEqual(other.IgnoredKeys)
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
                hashCode = (hashCode * 397) ^ NetworkLayers.GetEnumerableHashCode();
                hashCode = (hashCode * 397) ^ LearningRate.GetHashCode();
                hashCode = (hashCode * 397) ^ LearningMomentum.GetHashCode();
                hashCode = (hashCode * 397) ^ ValuePadding.GetHashCode();
                hashCode = (hashCode * 397) ^ AccurateCalculations.GetHashCode();
                hashCode = (hashCode * 397) ^ AverageAmount;
                hashCode = (hashCode * 397) ^ TrainAverageInParallel.GetHashCode();
                return hashCode;
            }
        }
    }
}