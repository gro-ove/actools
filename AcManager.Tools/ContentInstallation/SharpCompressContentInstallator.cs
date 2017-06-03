using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AcManager.Tools.ContentInstallation {
    internal class SharpCompressContentInstallator : ContentInstallatorBase {
        public static async Task<IAdditionalContentInstallator> Create(string filename, ContentInstallationParams installationParams, CancellationToken cancellation) {
            var result = new SharpCompressContentInstallator(filename, installationParams);
            await result.CreateExtractorAsync();
            return result;
        }

        [CanBeNull]
        private IArchive _extractor;

        public string Filename { get; }

        private SharpCompressContentInstallator(string filename, ContentInstallationParams installationParams) : base(installationParams) {
            Filename = filename;
        }

        private async Task CreateExtractorAsync() {
            try {
                _extractor = await Task.Run(() => CreateExtractor(Filename, Password));
            } catch (PasswordException) {
                IsPasswordRequired = true;
                DisposeHelper.Dispose(ref _extractor);
            } catch (Exception e) {
                Logging.Warning(e);
                IsNotSupported = true;
                NotSupportedMessage = e.Message;
                DisposeHelper.Dispose(ref _extractor);
            }
        }

        public override bool IsPasswordCorrect => !IsPasswordRequired || _extractor != null;

        protected override string GetBaseId() {
            var id = Path.GetFileNameWithoutExtension(Filename)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        private class SimpleFileInfo : IFileInfo {
            private readonly IEntry _archiveEntry;

            public SimpleFileInfo(IEntry archiveEntry) {
                _archiveEntry = archiveEntry;
            }

            public string Key => _archiveEntry.Key.Replace('/', '\\');
            public long Size => _archiveEntry.Size;

            public virtual Task<byte[]> ReadAsync() {
                throw new NotSupportedException();
            }

            public virtual bool IsAvailable() {
                throw new NotSupportedException();
            }

            public virtual Task CopyToAsync(string destination) {
                throw new NotSupportedException();
            }
        }

        private class ArchiveFileInfo : SimpleFileInfo {
            private readonly IArchiveEntry _archiveEntry;

            // for solid archives
            private readonly Func<string, byte[]> _reader;
            private readonly Func<string, bool> _availableCheck;

            public ArchiveFileInfo(IArchiveEntry archiveEntry, [CanBeNull] Func<string, byte[]> reader,
                    Func<string, bool> availableCheck) : base(archiveEntry) {
                _archiveEntry = archiveEntry;
                _reader = reader;
                _availableCheck = availableCheck;
            }

            public override async Task<byte[]> ReadAsync() {
                if (_reader != null) {
                    return await Task.Run(() => _reader(_archiveEntry.Key)).ConfigureAwait(false);
                }

                using (var memory = new MemoryStream())
                using (var stream = _archiveEntry.OpenEntryStream()) {
                    await stream.CopyToAsync(memory);
                    return memory.ToArray();
                }
            }

            public override bool IsAvailable() {
                return _availableCheck?.Invoke(_archiveEntry.Key) != false;
            }

            public override async Task CopyToAsync(string destination) {
                using (var fileStream = new FileStream(destination, FileMode.Create))
                using (var stream = _archiveEntry.OpenEntryStream()) {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        private readonly object _readSolidSync = new object();
        private IReader _readSolid;

        private byte[] ReadSolidImmediately(string key) {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            lock (_readSolidSync) {
                var created = false;
                while (true) {
                    if (_readSolid == null) {
                        _readSolid = _extractor.ExtractAllEntries();
                        created = true;
                    }

                    while (_readSolid.MoveToNextEntry()) {
                        if (_readSolid.Entry.Key == key) {
                            using (var memory = new MemoryStream())
                            using (var stream = _readSolid.OpenEntryStream()) {
                                stream.CopyTo(memory);
                                return memory.ToArray();
                            }
                        }
                    }

                    // just went to the end of archive, so next time, let’s start from the beginning
                    DisposeHelper.Dispose(ref _readSolid);

                    if (created) {
                        // if we started from the beginning, file is not here
                        return null;
                    }

                }
            }
        }

        private List<string> _askedData;
        private Dictionary<string, byte[]> _preloadedData;

        private byte[] ReadSolid(string key) {
            if (_preloadedData != null && _preloadedData.TryGetValue(key, out byte[] data)) {
                return data;
            }

            if (_askedData == null) {
                _askedData = new List<string> { key };
            } else {
                _askedData.Add(key);
            }

            return null;
        }

        private bool CheckSolid(string key) {
            if (_preloadedData != null && _preloadedData.ContainsKey(key)) {
                return true;
            }

            if (_askedData == null) {
                _askedData = new List<string> { key };
            } else {
                _askedData.Add(key);
            }

            return false;
        }

        protected override async Task LoadMissingContents(CancellationToken cancellation) {
            if (_askedData == null) return;
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            if (_preloadedData == null) {
                _preloadedData = new Dictionary<string, byte[]>();
            }

            await Task.Run(() => {
                using (var reader = _extractor.ExtractAllEntries()) {
                    while (reader.MoveToNextEntry()) {
                        var key = reader.Entry.Key;
                        if (_askedData.Contains(key)) {
                            using (var memory = new MemoryStream())
                            using (var stream = reader.OpenEntryStream()) {
                                stream.CopyTo(memory);
                                _preloadedData[key] = memory.ToArray();
                            }

                            _askedData.Remove(key);
                            if (_askedData.Count == 0) break;
                        }
                    }
                }
            });
        }

        protected override Task<IEnumerable<IFileInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            return Task.FromResult(
                    _extractor.Entries.Where(x => !x.IsDirectory).Select(
                            x => (IFileInfo)new ArchiveFileInfo(x,
                                    _extractor.IsSolid ? ReadSolid : (Func<string, byte[]>)null,
                                    _extractor.IsSolid ? CheckSolid : (Func<string, bool>)null)));
        }

        protected override async Task CopyFileEntries(CopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            if (!_extractor.IsSolid) {
                await base.CopyFileEntries(callback, progress, cancellation).ConfigureAwait(false);
                return;
            }

            await Task.Run(() => {
                using (var reader = _extractor.ExtractAllEntries()) {
                    var i = 0;
                    var count = _extractor.Entries.Count();

                    while (reader.MoveToNextEntry()) {
                        i++;

                        var entry = new SimpleFileInfo(reader.Entry);
                        var destination = callback(entry);
                        if (destination != null) {
                            FileUtils.EnsureFileDirectoryExists(destination);
                            progress?.Report(Path.GetFileName(destination), i, count);
                            reader.WriteEntryTo(destination);
                            if (cancellation.IsCancellationRequested) return;
                        }
                    }
                }
            });
        }

        public override Task TrySetPasswordAsync(string password, CancellationToken cancellation) {
            Password = password;
            return CreateExtractorAsync();
        }

        [NotNull]
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
            DisposeHelper.Dispose(ref _readSolid);
            DisposeHelper.Dispose(ref _extractor);
            GCHelper.CleanUp();
        }
    }
}
