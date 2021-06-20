using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public delegate void CarLodGeneratorResultCallback([NotNull] string key, [NotNull] string filename, [NotNull] string checksum);
}