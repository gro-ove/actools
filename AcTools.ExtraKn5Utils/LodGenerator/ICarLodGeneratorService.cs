using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public interface ICarLodGeneratorService {
        [NotNull]
        Task GenerateLodAsync([NotNull] string configuration, [NotNull] string inputFile, [NotNull] string outputFile,
                [CanBeNull] IProgress<double?> progress = null, CancellationToken cancellationToken = default);
    }
}