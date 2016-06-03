using System.IO;
using AcTools.AcdFile;
using AcTools.Utils;

namespace AcTools.DataFile {
    public abstract class AbstractDataFile {
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

                if (acd.Entries.ContainsKey(UnpackedFilename)) {
                    ParseString(acd.Entries[UnpackedFilename].ToString());
                } else {
                    Clear();
                }
            }
        }

        public void Save(string filename, bool backup = false) {
            if (SourceFilename == filename) {
                Save(backup);
                return;
            }

            if (UnpackedFilename != null || SourceFilename != null) {
                throw new InvalidDataException();
            }

            SaveAs(filename, backup);
        }

        public void SaveAs(string filename, bool backup = false) {
            if (SourceFilename == filename) {
                Save(backup);
                return;
            }

            if (File.Exists(filename) && backup) {
                FileUtils.Recycle(filename);
            }

            File.WriteAllText(filename, Stringify());
        }

        public void Save(bool backup = false) {
            if (UnpackedFilename == null || SourceFilename == null) {
                throw new InvalidDataException();
            }

            var data = Stringify();
            if (Mode == StorageMode.UnpackedFile) {
                if (File.Exists(SourceFilename) && backup) {
                    FileUtils.Recycle(SourceFilename);
                }
                File.WriteAllText(SourceFilename, data);
            } else {
                var acd = Acd.FromFile(SourceFilename);
                acd.SetEntry(UnpackedFilename, data);

                if (File.Exists(SourceFilename)) {
                    if (backup) {
                        FileUtils.Recycle(SourceFilename);
                    } else {
                        File.Delete(SourceFilename);
                    }
                }

                acd.Save(SourceFilename);
            }
        }

        public bool Exists() {
            return File.Exists(SourceFilename);
        }

        protected abstract void ParseString(string file);

        public abstract void Clear();

        public abstract string Stringify();
    }
}