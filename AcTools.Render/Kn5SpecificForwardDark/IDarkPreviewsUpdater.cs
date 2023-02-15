using System;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public interface IDarkPreviewsUpdater : IDisposable {
        void Shot([NotNull] string carId, [NotNull] string skinId, string destination = null, DataWrapper carData = null, 
                ImageUtils.ImageInformation information = null, Action callback = null);

        Task ShotAsync([NotNull] string carId, [NotNull] string skinId, string destination = null, DataWrapper carData = null,
                ImageUtils.ImageInformation information = null, Action callback = null, Func<bool> shutdownCheck = null, 
                CancellationToken cancellation = default(CancellationToken));

        void SetOptions(DarkPreviewsOptions options);
        
        Task WaitForProcessing();
    }
}