using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    [NotNull]
    public delegate void FlexibleLoaderReportDestinationCallback([CanBeNull] string destination);
}