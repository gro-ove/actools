using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Render.Base;

namespace AcManager.CustomShowroom {
    public interface ICustomShowroomShots {
        event EventHandler<CancelEventArgs> PreviewScreenshot;
        Size DefaultSize { get; }
        Task ShotAsync(Size size, bool downscale, string filename, RendererShotFormat format, CancellationToken cancellationToken);
        Task SplitShotAsync(Size size, bool downscale, string filename, RendererShotFormat format, CancellationToken cancellationToken);
    }
}