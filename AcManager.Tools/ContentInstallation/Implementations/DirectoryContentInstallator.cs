using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ContentInstallation.Implementations {
    internal class DirectoryContentInstallator : ContentInstallatorBase {
        public string Directory { get; }

        private DirectoryContentInstallator(string directory, ContentInstallationParams installationParams) : base(installationParams) {
            Directory = directory;
        }

        public static Task<IAdditionalContentInstallator> Create(string directory, ContentInstallationParams installationParams,
                CancellationToken cancellation) {
            return Task.FromResult((IAdditionalContentInstallator)new DirectoryContentInstallator(directory, installationParams));
        }

        protected override string GetBaseId() {
            var id = Path.GetFileName(Directory)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        protected override string GetBaseName() {
            return Path.GetFileName(Directory);
        }

        private class InnerDirectoryInfo : IDirectoryInfo {
            private readonly string _directory, _filename;

            public InnerDirectoryInfo(string directory, string filename) {
                _directory = directory;
                _filename = filename;
            }

            public string Key => FileUtils.GetRelativePath(_filename, _directory);
        }

        private class InnerFileInfo : IFileInfo  {
            private readonly string _directory, _filename;

            public InnerFileInfo(string directory, string filename) {
                _directory = directory;
                _filename = filename;
            }

            public string Key => FileUtils.GetRelativePath(_filename, _directory);

            private long? _size;
            public long Size {
                get {
                    try {
                        return _size ?? (_size = new FileInfo(_filename).Length).Value;
                    } catch (Exception e) {
                        Logging.Warning(e.Message);
                        _size = 0;
                        return _size.Value;
                    }
                }
            }

            public async Task<byte[]> ReadAsync() {
                using (var input = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
                    var result = new byte[input.Length];
                    await input.ReadAsync(result, 0, (int)input.Length);
                    return result;
                }
            }

            public bool IsAvailable() {
                return true;
            }

            public async Task CopyToAsync(string destination) {
                if (!File.Exists(_filename)) {
                    throw new FileNotFoundException(ToolsStrings.DirectoryInstallator_FileNotFound, _filename);
                }

                using (var input = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (var output = new FileStream(destination, FileMode.Create)) {
                    await input.CopyToAsync(output);
                }
            }
        }

        protected override Task<IEnumerable<IFileOrDirectoryInfo>> GetFileEntriesAsync(CancellationToken cancellation) {
            return Task.Run(() => (IEnumerable<IFileOrDirectoryInfo>)FileUtils
                    .GetFilesRecursive(Directory).Select(x => (IFileOrDirectoryInfo)new InnerFileInfo(Directory, x))
                    .Concat(FileUtils.GetDirectoriesRecursive(Directory).Select(x => (IDirectoryInfo)new InnerDirectoryInfo(Directory, x))).ToList());
        }

        protected override Task LoadMissingContents(CancellationToken cancellation) {
            throw new NotSupportedException();
        }
    }
}