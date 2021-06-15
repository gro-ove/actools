using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public class CarLodGeneratorProgressUpdate {
        [NotNull]
        public string Key = string.Empty;

        [CanBeNull]
        public double? Value;
    }
}