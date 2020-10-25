using System.IO;
using AcTools.Utils;

namespace AcTools.DataFile {
    public class DataDirectoryWrapper : DataWrapperBase {
        private readonly string _directory;

        public DataDirectoryWrapper(string directory) {
            if (!Directory.Exists(directory)) {
                throw new DirectoryNotFoundException(directory);
            }

            _directory = directory;
        }

        public override string Location => _directory;

        public override bool IsEmpty => false;

        public override bool IsPacked => false;

        public override string GetData(string name) {
            var filename = Path.Combine(_directory, name);
            return File.Exists(filename) ? File.ReadAllText(filename) : null;
        }

        public override bool Contains(string name) {
            var filename = Path.Combine(_directory, name);
            return File.Exists(filename);
        }

        protected override void RefreshOverride(string name) {}

        protected override void SetDataOverride(string name, string data, bool recycleOriginal = false) {
            var filename = Path.Combine(_directory, name);
            if (recycleOriginal) {
                using (var f = FileUtils.RecycleOriginal(filename)) {
                    try {
                        File.WriteAllText(f.Filename, data);
                    } catch {
                        FileUtils.TryToDelete(f.Filename);
                        throw;
                    }
                }
            } else {
                File.WriteAllText(filename, data);
            }
        }

        protected override void DeleteOverride(string name, bool recycleOriginal = false) {
            var filename = Path.Combine(_directory, name);
            if (recycleOriginal) {
                FileUtils.Recycle(filename);
            } else if (File.Exists(filename)) {
                File.Delete(filename);
            }
        }
    }
}