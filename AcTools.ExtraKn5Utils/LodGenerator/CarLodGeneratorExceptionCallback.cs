using System;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public delegate void CarLodGeneratorExceptionCallback([NotNull] string key, [NotNull] Exception exception);
}