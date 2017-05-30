using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    internal class ZipContentInstallator : ContentInstallatorBase {
        public static async Task<IAdditionalContentInstallator> Create(string filename, ContentInstallationParams installationParams) {
            var result = new ZipContentInstallator(filename, installationParams);
            await result.CreateExtractorAsync();
            return result;
        }

        [CanBeNull]
        private ZipArchive _extractor;

        public string Filename { get; }

        private ZipContentInstallator(string filename, ContentInstallationParams installationParams) : base(installationParams) {
            Filename = filename;
        }

        private async Task CreateExtractorAsync() {
            try {
                _extractor = await Task.Run(() => ZipFile.OpenRead(Filename));
            } catch (Exception) {
                DisposeHelper.Dispose(ref _extractor);
            }
        }

        protected override string GetBaseId() {
            var id = Path.GetFileNameWithoutExtension(Filename)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        private class ArchiveFileInfo : IFileInfo {
            private readonly ZipArchiveEntry _archiveEntry;

            public ArchiveFileInfo(ZipArchiveEntry archiveEntry) {
                _archiveEntry = archiveEntry;
            }

            public string Key => _archiveEntry.FullName.Replace('/', '\\');
            public long Size => _archiveEntry.Length;

            public async Task<byte[]> ReadAsync() {
                using (var memory = new MemoryStream())
                using (var stream = _archiveEntry.Open()) {
                    await stream.CopyToAsync(memory);
                    return memory.ToArray();
                }
            }

            public async Task CopyToAsync(string destination) {
                using (var fileStream = new FileStream(destination, FileMode.Create))
                using (var stream = _archiveEntry.Open()) {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        protected override Task<IEnumerable<IFileInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);
            return Task.FromResult(_extractor.Entries.Where(x => !x.FullName.EndsWith("\\") && !x.FullName.EndsWith("/"))
                                             .Select(x => (IFileInfo)new ArchiveFileInfo(x)));
        }

        protected override Task LoadMissingContents(CancellationToken cancellation) {
            throw new NotSupportedException();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _extractor);
            GCHelper.CleanUp();
        }
    }
}