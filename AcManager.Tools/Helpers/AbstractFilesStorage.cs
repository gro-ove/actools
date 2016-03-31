using System.IO;

namespace AcManager.Tools.Helpers {
    public class AbstractFilesStorage : AbstractSubdirectoryWatcherProvider {
        private readonly string _path;

        protected AbstractFilesStorage(string path) {
            _path = path;
        }

        public string CombineFilename(params string[] parts) {
            return parts.Length == 0 ? _path : Path.Combine(_path, Path.Combine(parts));
        }

        public string EnsureDirectory(params string[] parts) {
            var directory = CombineFilename(parts);

            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        public string GetFilename(params string[] parts) {
            var filename = CombineFilename(parts);
            EnsureDirectory(Path.GetDirectoryName(filename));
            return filename;
        }

        public string GetDirectory(params string[] file) {
            var filename = Path.Combine(_path, Path.Combine(file));
            EnsureDirectory(filename);
            return filename;
        }

        protected override string GetSubdirectoryFilename(string name) {
            return Path.Combine(_path, name);
        }
    }
}
