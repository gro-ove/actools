using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AcTools.AcdFile {
    public class Acd {
        public readonly string OriginalFilename;

        private Acd(string filename) {
            OriginalFilename = filename;
            Entries = new Dictionary<string, AcdEntry>(60);
        }

        public Dictionary<string, AcdEntry> Entries;

        public void SetEntry(string entryName, byte[] entryData) {
            Entries[entryName] = new AcdEntry {
                Name = entryName,
                Data = entryData
            };
        }

        public void SetEntry(string entryName, string entryData) {
            SetEntry(entryName, Encoding.UTF8.GetBytes(entryData));
        }

        public static Acd FromFile(string filename) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var acd = new Acd(filename);
            using (var reader = new AcdReader(filename)) {
                acd.FromFile_Entries(reader);
            }

            return acd;
        }

        private void FromFile_Entries(AcdReader reader) {
            while (reader.BaseStream.Position < reader.BaseStream.Length) {
                var entry = reader.ReadEntry();
                Entries[entry.Name] = entry;
            }
        }

        public void Save(string filename) {
            using (var writer = new AcdWriter(filename)) {
                foreach (var entry in Entries.Values) {
                    writer.Write(entry);
                }
            }
        }

        public static Acd FromDirectory(string dir) {
            if (!Directory.Exists(dir)) {
                throw new DirectoryNotFoundException(dir);
            }

            var acd = new Acd(dir);
            foreach (var file in Directory.GetFiles(dir)) {
                acd.SetEntry(Path.GetFileName(file), File.ReadAllBytes(file));
            }

            return acd;
        }

        public void ExportDirectory(string dir) {
            Directory.CreateDirectory(dir);
            foreach (var entry in Entries.Values) {
                File.WriteAllBytes(Path.Combine(dir, entry.Name), entry.Data);
            }
        }
    }
}
