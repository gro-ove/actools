using System.ComponentModel;
using System.IO;

namespace AcManager.Tools.Helpers {
    public class AbstractFilesStorage : AbstractSubdirectoryWatcherProvider {
        protected string RootDirectory { get; }

        protected AbstractFilesStorage(string path) {
            RootDirectory = path;
        }

        public string Combine(params string[] parts) {
            return parts.Length == 0 ? RootDirectory : Path.Combine(RootDirectory, Path.Combine(parts));
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
            return name == null ? RootDirectory : Path.Combine(RootDirectory, name);
        }
    }
}
