using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public interface ICarLodGeneratorService {
        [NotNull]
        Task<string> GenerateLodAsync([NotNull] string stageId, [NotNull] string inputFile, string modelChecksum,
                [CanBeNull] IProgress<double?> progress = null, CancellationToken cancellationToken = default);
    }
}