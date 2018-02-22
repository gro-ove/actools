using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ILoaderFactory {
        [CanBeNull]
        ILoader Create([NotNull] string url);
    }
}