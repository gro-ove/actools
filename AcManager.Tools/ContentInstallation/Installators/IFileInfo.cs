using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    public interface IFileInfo {
        string Key { get; }

        long Size { get; }

        /// <summary>
        /// Read data. When returns null, call IAdditionalContentInstallator.LoadMissingContents() and then
        /// use ReadAsync() again. This way, solid archives should work faster.
        /// </summary>
        [ItemCanBeNull]
        Task<byte[]> ReadAsync();

        /// <summary>
        /// Is data available now or it’s better to wait for the second pass? If called, data will be prepared
        /// for it (it’s like ReadAsync(), but without reading, if data is not available).
        /// </summary>
        bool IsAvailable();

        Task CopyToAsync(string destination);
    }
}