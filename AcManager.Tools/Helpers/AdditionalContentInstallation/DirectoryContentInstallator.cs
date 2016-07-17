using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    internal class DirectoryContentInstallator : BaseAdditionalContentInstallator {
        public string Directory { get; }

        private DirectoryContentInstallator(string directory) {
            Directory = directory;
        }

        public static Task<IAdditionalContentInstallator> Create(string directory) {
            return Task.FromResult((IAdditionalContentInstallator)new DirectoryContentInstallator(directory));
        }

        protected override string GetBaseId() {
            var id = Path.GetFileName(Directory)?.ToLower();
            return AcStringValues.IsAppropriateId(id) ? id : null;
        }

        private class InnerFileInfo : IFileInfo  {
            private readonly string _directory, _filename;

            public InnerFileInfo(string directory, string filename) {
                _directory = directory;
                _filename = filename;
            }

            public string Filename => FileUtils.GetRelativePath(_filename, _directory);

            public async Task<byte[]> ReadAsync() {
                using (var input = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
                    var result = new byte[input.Length];
                    await input.ReadAsync(result, 0, (int)input.Length);
                    return result;
                }
            }

            public async Task CopyTo(string destination) {
                if (!File.Exists(_filename)) {
                    throw new FileNotFoundException(Resources.DirectoryInstallator_FileNotFound, _filename);
                }

                using (var input = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (var output = new FileStream(destination, FileMode.Create)) {
                    await input.CopyToAsync(output);
                }
            }
        }

        protected override Task<IEnumerable<IFileInfo>> GetFileEntriesAsync() {
            return Task.Run(() => {
                var result = FileUtils.GetFiles(Directory).Select(x => (IFileInfo)new InnerFileInfo(Directory, x)).ToList();
                return (IEnumerable<IFileInfo>)result;
            });
        }
    }
}