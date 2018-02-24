using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ILoaderFactory {
        bool Test(string url);

        [CanBeNull]
        ILoader Create([NotNull] string url);
    }
}