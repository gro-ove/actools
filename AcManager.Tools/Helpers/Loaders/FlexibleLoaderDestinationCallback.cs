using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    [NotNull]
    public delegate FlexibleLoaderDestination FlexibleLoaderDestinationCallback([CanBeNull] string downloadUrl, [NotNull] FlexibleLoaderMetaInformation information);
}