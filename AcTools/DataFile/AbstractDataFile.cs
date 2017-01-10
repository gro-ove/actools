using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AcTools.AcdFile;
using AcTools.Utils;

namespace AcTools.DataFile {
    public abstract class AbstractDataFile {
        public static ISyntaxErrorsCatcher ErrorsCatcher { get; set; }

        public enum StorageMode {
            UnpackedFile,
            AcdFile
        }
        
        public readonly string UnpackedFilename;

        public readonly StorageMode Mode;
        public readonly string SourceFilename;

        protected AbstractDataFile(string carDir, string filename, Acd loadedAcd) {
            UnpackedFilename = filename;

            var acdFile = Path.Combine(carDir, "data.acd");
            if (loadedAcd != null || File.Exists(acdFile)) {
                Mode = StorageMode.AcdFile;
                SourceFilename = acdFile;
            } else {
                Mode = StorageMode.UnpackedFile;
                SourceFilename = Path.Combine(carDir, "data", filename);
            }

            Load(loadedAcd);
        }

        protected AbstractDataFile(string carDir, string filename) : this(carDir, filename, null) {}

        protected AbstractDataFile(string filename) {
            UnpackedFilename = filename;
            Mode = StorageMode.UnpackedFile;
            if (filename == null) {
                SourceFilename = null;
            } else {
                SourceFilename = Path.Combine(filename);
                Load();
            }
        }

        protected AbstractDataFile() : this(null) {}

        public void Load(Acd acd = null) {
            if (Mode == StorageMode.UnpackedFile) {
                if (File.Exists(SourceFilename)) {
                    ParseString(File.ReadAllText(SourceFilename));
                } else {
                    Clear();
                }
            } else {
                if (acd == null) {
                    acd = Acd.FromFile(SourceFilename);
                }

                var entry = acd.GetEntry(UnpackedFilename);
                if (entry != null) {
                    ParseString(entry.ToString());
                } else {
                    Clear();
                }
            }
        }

        public Task SaveAsync(string filename = null, bool backup = false) {
            if (filename == null || SourceFilename == filename) {
                if (Mode != StorageMode.UnpackedFile) {
                    UpdateAcd(backup);
                    return Task.Delay(0);
                }

                filename = SourceFilename;
            }

            return SaveToAsync(filename, backup);
        }

        public void Save(string filename, bool backup = false) {
            if (filename == null || SourceFilename == filename) {
                if (Mode != StorageMode.UnpackedFile) {
                    UpdateAcd(backup);
                    return;
                }

                filename = SourceFilename;
            }

            SaveTo(filename, backup);
        }

        public void Save(bool backup = false) {
            Save(null, backup);
        }

        protected void UpdateAcd(bool backup) {
            if (UnpackedFilename == null || SourceFilename == null) {
                throw new Exception("File wasn’t loaded to be saved like this");
            }

            var acd = Acd.FromFile(SourceFilename);
            acd.SetEntry(UnpackedFilename, Stringify());

            if (File.Exists(SourceFilename)) {
                if (backup) {
                    FileUtils.Recycle(SourceFilename);
                } else {
                    File.Delete(SourceFilename);
                }
            }

            acd.Save(SourceFilename);
        }

        protected virtual void SaveTo(string filename, bool backup) {
            if (File.Exists(filename) && backup) {
                FileUtils.Recycle(filename);
            }
            File.WriteAllText(filename, Stringify());
        }

        protected virtual Task SaveToAsync(string filename, bool backup) {
            if (File.Exists(filename) && backup) {
                FileUtils.Recycle(filename);
            }
            return FileUtils.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(Stringify()));
        }

        [Obsolete]
        public bool Exists() {
            return File.Exists(SourceFilename);
        }

        protected abstract void ParseString(string file);

        public abstract void Clear();

        public abstract string Stringify();
    }
}