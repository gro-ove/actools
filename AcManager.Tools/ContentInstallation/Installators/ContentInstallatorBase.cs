using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Entries;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    public abstract class ContentInstallatorBase : IAdditionalContentInstallator {
        [NotNull]
        public ContentInstallationParams InstallationParams { get; }

        protected ContentInstallatorBase([NotNull] ContentInstallationParams installationParams) {
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

        [CanBeNull]
        protected abstract string GetBaseName();

        [ItemCanBeNull]
        protected abstract Task<IEnumerable<IFileOrDirectoryInfo>> GetFileEntriesAsync(CancellationToken cancellation);

        public async Task<IReadOnlyList<ContentEntryBase>> GetEntriesAsync(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync(cancellation))?.OfType<IFileInfo>().ToList();
            if (list == null) return null;

            for (var i = 0; i < 2; i++) {
                if (i != 0) {
                    await LoadMissingContents(cancellation);
                    if (cancellation.IsCancellationRequested) return null;
                }

                var result = await new ContentScanner(InstallationParams).GetEntriesAsync(list, GetBaseId(), GetBaseName(), progress, cancellation);
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

        protected virtual async Task CopyFileEntries([NotNull] ICopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync(cancellation))?.ToList();
            if (list == null) return;

            var files = list.OfType<IFileInfo>().ToList();
            for (var i = 0; i < files.Count; i++) {
                var fileInfo = files[i];
                var destination = callback.File(fileInfo);
                if (destination != null) {
                    FileUtils.EnsureFileDirectoryExists(destination);
                    progress?.Report(Path.GetFileName(destination), i, files.Count);
                    await fileInfo.CopyToAsync(destination);
                    NewFilesReporter.RegisterNewFile(destination);
                    if (cancellation.IsCancellationRequested) return;
                }
            }

            var directories = list.OfType<IDirectoryInfo>().ToList();
            for (var i = 0; i < directories.Count; i++) {
                var directoryInfo = directories[i];
                var destination = callback.Directory(directoryInfo);
                if (destination != null) {
                    FileUtils.EnsureDirectoryExists(destination);
                }
            }
        }

        public async Task InstallAsync([NotNull] ICopyCallback callback,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await CopyFileEntries(callback, progress, cancellation);
        }
    }
}