using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SharpCompress.Archive;
using SharpCompress.Common;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    internal class ArchiveContentInstallator : BaseAdditionalContentInstallator {
        public static async Task<IAdditionalContentInstallator> Create(string filename) {
            var result = new ArchiveContentInstallator(filename);
            await result.CreateExtractorAsync();
            return result;
        }

        [CanBeNull]
        private IArchive _extractor;

        public string Filename { get; }

        private ArchiveContentInstallator(string filename) {
            Filename = filename;
        }

        public override bool IsPasswordCorrect => !IsPasswordRequired || _extractor != null;

        protected override string GetBaseId() {
            var id = Path.GetFileNameWithoutExtension(Filename)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        private class ArchiveFileInfo : IFileInfo {
            private readonly IArchiveEntry _archiveEntry;

            public ArchiveFileInfo(IArchiveEntry archiveEntry) {
                _archiveEntry = archiveEntry;
            }
            
            public string Filename => _archiveEntry.GetFilename();

            public Task<byte[]> ReadAsync() {
                return _archiveEntry.ExtractFileAsync();
            }

            public Task CopyTo(string destination) {
                using (var fileStream = new FileStream(destination, FileMode.Create))
                using (var stream = _archiveEntry.OpenEntryStream()) {
                    return stream.CopyToAsync(fileStream);
                }
            }
        }

        protected override Task<IEnumerable<IFileInfo>> GetFileEntriesAsync() {
            if (_extractor == null) throw new Exception("Extractor wasn't initialized");
            return Task.FromResult(
                    _extractor.Entries.Where(x => !x.IsDirectory).Select(x => (IFileInfo)new ArchiveFileInfo(x)));
        }

        private async Task CreateExtractorAsync() {
            try {
                _extractor = await Task.Run(() => CreateExtractor(Filename, Password));
            } catch (PasswordException) {
                IsPasswordRequired = true;
                DisposeHelper.Dispose(ref _extractor);
            }
        }

        public override Task TrySetPasswordAsync(string password) {
            Password = password;
            return CreateExtractorAsync();
        }

        private static IArchive CreateExtractor(string filename, string password) {
            try {
                var extractor = SharpCompressExtension.Open(filename, password);
                if (extractor.HasAnyEncryptedFiles()) {
                    throw new PasswordException(password == null ? "Password is required" :
                            "Password is invalid");
                }
                return extractor;
            } catch (CryptographicException) {
                throw new PasswordException(password == null ? "Password is required" :
                        "Password is invalid");
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _extractor);
            GC.Collect();
        }
    }
}
