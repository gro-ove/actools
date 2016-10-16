using System.ComponentModel;
using System.IO;

namespace AcManager.Tools.Helpers {
    public class AbstractFilesStorage : AbstractSubdirectoryWatcherProvider {
        private readonly string _path;

        protected AbstractFilesStorage(string path) {
            _path = path;
        }

        public string Combine(params string[] parts) {
            return parts.Length == 0 ? _path : Path.Combine(_path, Path.Combine(parts));
        }

        public string EnsureDirectory(params string[] parts) {
            var directory = Combine(parts);

            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        public string GetFilename([Localizable(false)] params string[] parts) {
            var filename = Combine(parts);
            EnsureDirectory(Path.GetDirectoryName(filename));
            return filename;
        }

        public string GetDirectory([Localizable(false)] params string[] file) {
            var filename = Combine(file);
            EnsureDirectory(filename);
            return filename;
        }

        protected override string GetSubdirectoryFilename(string name) {
            return Path.Combine(_path, name);
        }
    }
}
