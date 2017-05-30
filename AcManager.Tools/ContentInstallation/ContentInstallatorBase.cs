using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.ContentInstallation {
    public interface IFileInfo {
        string Key { get; }

        long Size { get; }

        /// <summary>
        /// Read data. When returns null, call IAdditionalContentInstallator.LoadMissingContents() and then
        /// use ReadAsync() again. This way, solid archives should work faster.
        /// </summary>
        [ItemCanBeNull]
        Task<byte[]> ReadAsync();

        Task CopyToAsync(string destination);
    }

    internal static class ContentInstallationExtension {
        [CanBeNull]
        public static IFileInfo GetByKey(this IEnumerable<IFileInfo> list, string key) {
            return list.FirstOrDefault(x => FileUtils.ArePathsEqual(x.Key, key));
        }
    }

    /// <summary>
    /// Takes file information and, if copy needed, returns destination path.
    /// </summary>
    [CanBeNull]
    public delegate string CopyCallback([NotNull] IFileInfo info);

    internal abstract class ContentInstallatorBase : IAdditionalContentInstallator {
        public ContentInstallationParams InstallationParams { get; }

        protected ContentInstallatorBase(ContentInstallationParams installationParams) {
            InstallationParams = installationParams;
        }

        public virtual Task TrySetPasswordAsync(string password, CancellationToken cancellation) {
            throw new NotSupportedException();
        }

        public virtual void Dispose() {}

        public string Password { get; protected set; }

        public bool IsNotSupported { get; protected set; }

        public string NotSupportedMessage { get; protected set; }

        public bool IsPasswordRequired { get; protected set; }

        public virtual bool IsPasswordCorrect => true;

        [CanBeNull]
        protected abstract string GetBaseId();

        [ItemCanBeNull]
        protected abstract Task<IEnumerable<IFileInfo>> GetFileEntriesAsync(CancellationToken cancellation);

        public async Task<IReadOnlyList<ContentEntryBase>> GetEntriesAsync(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync(cancellation))?.ToList();
            if (list == null) return null;

            for (var i = 0; i < 2; i++) {
                if (i != 0) {
                    await LoadMissingContents(cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }

                var result = await ContentScanner.GetEntriesAsync(list, GetBaseId(), progress, cancellation);
                if (result == null || cancellation.IsCancellationRequested) return null;

                if (result.MissingContent) {
                    if (i == 0) continue;

                    IsNotSupported = true;
                    NotSupportedMessage = "Internal error";
                    return null;
                }

                if (result.Result.Count == 0 && result.Exception != null) {
                    IsNotSupported = true;
                    NotSupportedMessage = result.Exception.Message;
                    return null;
                }

                return result.Result;
            }

            return null;
        }

        protected abstract Task LoadMissingContents(CancellationToken cancellation);

        protected virtual async Task CopyFileEntries([NotNull] CopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync(cancellation))?.ToList();
            if (list == null) return;

            for (var i = 0; i < list.Count; i++) {
                var fileInfo = list[i];
                var destination = callback(fileInfo);
                if (destination != null) {
                    FileUtils.EnsureFileDirectoryExists(destination);
                    progress?.Report(Path.GetFileName(destination), i, list.Count);
                    await fileInfo.CopyToAsync(destination);
                    if (cancellation.IsCancellationRequested) return;
                }
            }
        }

        public async Task InstallEntryToAsync([NotNull] CopyCallback callback,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await CopyFileEntries(callback, progress, cancellation);
        }

        public async Task InstallEntryToAsync(ContentEntryBase entryBase,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var callback = await entryBase.GetCopyCallback(cancellation);
            if (callback == null) return;
            await CopyFileEntries(callback, progress, cancellation);
        }
    }
}