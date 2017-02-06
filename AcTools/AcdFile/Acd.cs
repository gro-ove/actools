using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    public class Acd {
        [CanBeNull]
        private readonly string _packedFile;

        [CanBeNull]
        private readonly string _unpackedDirectory;

        [CanBeNull]
        private byte[] _packedBytes;

        public bool IsPacked => _packedFile != null;

        private Acd(string packedFile, string unpackedDirectory) {
            _packedBytes = packedFile == null ? null : File.ReadAllBytes(packedFile);
            _packedFile = packedFile;
            _unpackedDirectory = unpackedDirectory;
            _entries = new Dictionary<string, AcdEntry>(10);
        }

        private readonly Dictionary<string, AcdEntry> _entries;

        [CanBeNull]
        public AcdEntry GetEntry(string entryName) {
            AcdEntry entry;

            if (!_entries.TryGetValue(entryName, out entry)) {
                if (_unpackedDirectory != null) {
                    var filename = Path.Combine(_unpackedDirectory, entryName);
                    entry = File.Exists(filename) ? new AcdEntry {
                        Name = entryName,
                        Data =  File.ReadAllBytes(filename)
                    } : new AcdEntry {
                        Name = entryName,
                        Data = new byte[0]
                    };
                } else {
                    var data = ReadPacked(entryName);
                    entry = data != null ? new AcdEntry {
                        Name = entryName,
                        Data = data
                    } : new AcdEntry {
                        Name = entryName,
                        Data = new byte[0]
                    };
                }

                _entries[entryName] = entry;
            }

            return entry;
        }

        [CanBeNull]
        private byte[] ReadPacked(string entryName) {
            if (_packedBytes == null) {
                if (_packedFile == null) return null;
                _packedBytes = File.ReadAllBytes(_packedFile);
            }

            using (var stream = new MemoryStream(_packedBytes))
            using (var reader = new AcdReader(_packedFile, stream)) {
                return reader.ReadEntryData(entryName);
            }
        }

        private void SetEntry(string entryName, byte[] entryData) {
            _entries[entryName] = new AcdEntry {
                Name = entryName,
                Data = entryData
            };
        }

        public void SetEntry(string entryName, string entryData) {
            SetEntry(entryName, Encoding.UTF8.GetBytes(entryData));
        }

        public static Acd FromFile(string filename) {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            return new Acd(filename, null);
        }

        public void EnsureFullyLoaded() {
            if (_packedFile != null) {
                using (var reader = new AcdReader(_packedFile)) {
                    while (reader.BaseStream.Position < reader.BaseStream.Length) {
                        var entry = reader.ReadEntry();
                        if (!_entries.ContainsKey(entry.Name)) {
                            _entries[entry.Name] = entry;
                        }
                    }
                }
            } else if (_unpackedDirectory != null) {
                foreach (var file in Directory.GetFiles(_unpackedDirectory)) {
                    var name = Path.GetFileName(file);
                    if (name != null && (!_entries.ContainsKey(name) || _entries[name] == null)) {
                        _entries[name] = new AcdEntry {
                            Name = name,
                            Data = File.ReadAllBytes(file)
                        };
                    }
                }
            }
        }
        
        public void Save([NotNull] string filename) {
            if (filename == null) throw new Exception("Filename not specified (shouldn’t happen)");

            EnsureFullyLoaded();
            using (var writer = new AcdWriter(filename)) {
                foreach (var entry in _entries.Values) {
                    writer.Write(entry);
                }
            }
        }

        /// <summary>
        /// Works only if loaded from directory!
        /// </summary>
        public string GetFilename(string entryName) {
            if (IsPacked) throw new Exception("Can’t do, packed");
            if (_unpackedDirectory == null) throw new Exception("Can’t do, unpacked directory not set");
            return Path.Combine(_unpackedDirectory, entryName);
        }

        public static Acd FromDirectory(string dir) {
            if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
            return new Acd(null, dir);
        }

        public void ExportDirectory(string dir) {
            EnsureFullyLoaded();
            foreach (var entry in _entries.Values) {
                var destination = Path.Combine(dir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? "");
                File.WriteAllBytes(destination, entry.Data);
            }
        }

        public static bool IsAvailable() {
            try {
                AcdEncryption.CreateKey(null);
            } catch (NotImplementedException) {
                return false;
            } catch (NullReferenceException) {
                return true;
            }

            return true;
        }
    }
}
