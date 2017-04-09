using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace AcManager.Tools.ContentInstallation {
    internal class SharpCompressContentInstallator : BaseContentInstallator {
        public static async Task<IAdditionalContentInstallator> Create(string filename) {
            var result = new SharpCompressContentInstallator(filename);
            await result.CreateExtractorAsync();
            return result;
        }

        [CanBeNull]
        private IArchive _extractor;

        public string Filename { get; }

        private SharpCompressContentInstallator(string filename) {
            Filename = filename;
        }

        private async Task CreateExtractorAsync() {
            try {
                _extractor = await Task.Run(() => CreateExtractor(Filename, Password));
            } catch (PasswordException) {
                IsPasswordRequired = true;
                DisposeHelper.Dispose(ref _extractor);
            }
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

            public string Filename => _archiveEntry.Key.Replace('/', '\\');

            public async Task<byte[]> ReadAsync() {
                using (var memory = new MemoryStream())
                using (var stream = _archiveEntry.OpenEntryStream()) {
                    await stream.CopyToAsync(memory);
                    return memory.ToArray();
                }
            }

            public async Task CopyTo(string destination) {
                using (var fileStream = new FileStream(destination, FileMode.Create))
                using (var stream = _archiveEntry.OpenEntryStream()) {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        protected override Task<IEnumerable<IFileInfo>> GetFileEntriesAsync() {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);
            return Task.FromResult(
                    _extractor.Entries.Where(x => !x.IsDirectory).Select(x => (IFileInfo)new ArchiveFileInfo(x)));
        }

        public override Task TrySetPasswordAsync(string password) {
            Password = password;
            return CreateExtractorAsync();
        }

        private static IArchive CreateExtractor(string filename, string password) {
            try {
                var extractor = SharpCompressExtension.Open(filename, password);
                if (extractor.HasAnyEncryptedFiles()) {
                    throw new PasswordException(password == null ? ToolsStrings.ArchiveInstallator_PasswordIsRequired :
                            ToolsStrings.ArchiveInstallator_PasswordIsInvalid);
                }
                return extractor;
            } catch (CryptographicException) {
                throw new PasswordException(password == null ? ToolsStrings.ArchiveInstallator_PasswordIsRequired :
                        ToolsStrings.ArchiveInstallator_PasswordIsInvalid);
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _extractor);
            GCHelper.CleanUp();
        }
    }
}
