using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    [NotNull]
    public delegate FlexibleLoaderDestination FlexibleLoaderGetPreferredDestinationCallback(
            [CanBeNull] string downloadUrl, [NotNull] FlexibleLoaderMetaInformation information);
}