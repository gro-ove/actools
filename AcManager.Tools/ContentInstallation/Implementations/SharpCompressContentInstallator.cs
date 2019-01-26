using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
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

namespace AcManager.Tools.ContentInstallation.Implementations {
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

        protected override string GetBaseName() {
            return Path.GetFileNameWithoutExtension(Filename);
        }

        private class SimpleDirectoryInfo : IDirectoryInfo {
            private readonly IEntry _archiveEntry;

            public SimpleDirectoryInfo(IEntry archiveEntry) {
                _archiveEntry = archiveEntry;
            }

            public string Key => _archiveEntry.Key.Replace('/', '\\');
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

            public override Task<byte[]> ReadAsync() {
                if (_reader != null) {
                    return Task.Run(() => _reader(_archiveEntry.Key));
                }

                return Task.Run(() => {
                    using (var memory = new MemoryStream())
                    using (var stream = _archiveEntry.OpenEntryStream()) {
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                });
            }

            public override bool IsAvailable() {
                return _availableCheck?.Invoke(_archiveEntry.Key) != false;
            }

            public override Task CopyToAsync(string destination) {
                return Task.Run(() => {
                    using (var fileStream = new FileStream(destination, FileMode.Create))
                    using (var stream = _archiveEntry.OpenEntryStream()) {
                        stream.CopyTo(fileStream);
                    }
                });
            }
        }

        private IReader _readSolid;

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

        protected override Task<IEnumerable<IFileOrDirectoryInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            var extractor = _extractor;
            if (extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            return Task.FromResult(extractor.Entries.Select(x => x.IsDirectory ?
                    (IFileOrDirectoryInfo)new SimpleDirectoryInfo(x) :
                    new ArchiveFileInfo(x,
                            extractor.IsSolid ? ReadSolid : (Func<string, byte[]>)null,
                            extractor.IsSolid ? CheckSolid : (Func<string, bool>)null)));
        }

        protected override async Task CopyFileEntries(ICopyCallback callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_extractor == null) throw new Exception(ToolsStrings.ArchiveInstallator_InitializationFault);

            if (!_extractor.IsSolid) {
                await base.CopyFileEntries(callback, progress, cancellation).ConfigureAwait(false);
                return;
            }

            try {
                await Task.Run(() => {
                    using (var reader = _extractor.ExtractAllEntries()) {
                        var i = 0;
                        var count = _extractor.Entries.Count();

                        while (reader.MoveToNextEntry()) {
                            i++;

                            var readerEntry = reader.Entry;
                            if (readerEntry.IsDirectory) {
                                var entry = new SimpleDirectoryInfo(readerEntry);
                                var destination = callback.Directory(entry);
                                if (destination != null) {
                                    FileUtils.EnsureDirectoryExists(destination);
                                }
                            } else {
                                var entry = new SimpleFileInfo(readerEntry);
                                var destination = callback.File(entry);
                                if (destination != null) {
                                    FileUtils.EnsureFileDirectoryExists(destination);
                                    progress?.Report(Path.GetFileName(destination), i, count);
                                    reader.WriteEntryTo(destination);
                                    if (cancellation.IsCancellationRequested) return;
                                }
                            }
                        }
                    }
                });
            } catch (NullReferenceException) {
                throw new InformativeException("Unsupported archive; please, consider enabling CM’s 7-Zip plugin");
            }
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
