using System;
using System.IO;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public abstract class DataFileBase : IDataFile {
        public static ISyntaxErrorsCatcher ErrorsCatcher { get; set; }

        public string Name { get; private set; }
        public string Filename { get; private set; }

        protected DataFileBase([CanBeNull] string filename) {
            Filename = filename;
            Name = filename == null ? "" : Path.GetFileName(filename);
            Load();
        }

        protected DataFileBase() {
            Filename = null;
            Name = "";
        }

        [CanBeNull]
        public IDataReadWrapper Data { get; private set; }

        void IDataFile.Initialize(IDataReadWrapper data, string name, string filename) {
            Data = data;
            Name = name;
            Filename = filename;
            Load();
        }

        private void Load() {
            if (Data != null) {
                var data = Data.GetData(Name);
                if (data != null) {
                    ParseString(data);
                } else {
                    Clear();
                }
            } else if (Filename != null && File.Exists(Filename)) {
                ParseString(File.ReadAllText(Filename));
            } else {
                Clear();
            }
        }

        // Only for non-packed non-car data
        protected virtual void SaveToOverride(string filename, bool recycleOriginal) {
            if (recycleOriginal) {
                using (var f = FileUtils.RecycleOriginal(filename)) {
                    try {
                        File.WriteAllText(f.Filename, Stringify());
                    } catch {
                        FileUtils.TryToDelete(f.Filename);
                        throw;
                    }
                }
            } else {
                File.WriteAllText(filename, Stringify());
            }
        }

        public void Save([CanBeNull] string filename, bool recycleOriginal = false) {
            var data = Data;
            if (data != null) {
                (data as IDataWrapper ?? throw new Exception("Can’t change read-only data")).SetData(Name, Stringify(), recycleOriginal);
                return;
            }

            SaveToOverride(filename ?? Filename ?? throw new Exception("Filename not set"),
                    recycleOriginal);
        }

        public void Save(bool recycleOriginal = false) {
            Save(null, recycleOriginal);
        }

        protected abstract void ParseString(string file);
        public abstract void Clear();
        public abstract string Stringify();

        public override string ToString() {
            return Stringify();
        }
    }
}