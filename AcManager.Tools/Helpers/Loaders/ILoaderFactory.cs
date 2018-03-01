using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ILoaderFactory {
        [NotNull]
        Task<bool> TestAsync([NotNull] string url, CancellationToken cancellation);

        [NotNull, ItemCanBeNull]
        Task<ILoader> CreateAsync([NotNull] string url, CancellationToken cancellation);
    }
}