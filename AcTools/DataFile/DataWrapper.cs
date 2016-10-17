using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using AcTools.AcdFile;

namespace AcTools.DataFile {
    public interface IDataWrapper {
        IniFile GetIniFile([Localizable(false)] string name);

        LutDataFile GetLutFile(string name);

        RawDataFile GetRawFile(string name);
    }

    public class DataDirectoryWrapper : IDataWrapper {
        private readonly string _directory;

        public DataDirectoryWrapper(string directory) {
            if (!Directory.Exists(directory)) {
                throw new DirectoryNotFoundException(directory);
            }

            _directory = directory;
        }

        public IniFile GetIniFile(string name) {
            return new IniFile(Path.Combine(_directory, name));
        }

        public LutDataFile GetLutFile(string name) {
            return new LutDataFile(Path.Combine(_directory, name));
        }

        public RawDataFile GetRawFile(string name) {
            return new RawDataFile(Path.Combine(_directory, name));
        }
    }

    public class DataWrapper : IDataWrapper {
        private readonly string _carDirectory;
        private readonly Acd _acd;
        private readonly Dictionary<string, AbstractDataFile> _cache;

        private DataWrapper(string carDirectory) {
            _carDirectory = carDirectory;
            _cache = new Dictionary<string, AbstractDataFile>();

            var dataAcd = Path.Combine(carDirectory, "data.acd");
            if (File.Exists(dataAcd)) {
                _acd = Acd.FromFile(dataAcd);
                IsPacked = true;
            } else {
                var dataDirectory = Path.Combine(carDirectory, "data");
                if (Directory.Exists(dataDirectory)) {
                    _acd = Acd.FromDirectory(dataDirectory);
                }
            }
        }

        public bool IsPacked { get; }

        public bool IsEmpty => _acd == null;

        public IniFile GetIniFile([Localizable(false)] string name) {
            AbstractDataFile cached;
            if (_cache.TryGetValue(name, out cached)) return (IniFile)cached;

            var result = new IniFile(_carDirectory, name, _acd);
            _cache[name] = result;
            return result;
        }

        public LutDataFile GetLutFile(string name) {
            AbstractDataFile cached;
            if (_cache.TryGetValue(name, out cached)) return (LutDataFile)cached;

            var result = new LutDataFile(_carDirectory, name, _acd);
            _cache[name] = result;
            return result;
        }

        public RawDataFile GetRawFile(string name) {
            AbstractDataFile cached;
            if (_cache.TryGetValue(name, out cached)) return (RawDataFile)cached;

            var result = new RawDataFile(_carDirectory, name, _acd);
            _cache[name] = result;
            return result;
        }

        public static DataWrapper FromDirectory(string carDirectory) {
            if (!Directory.Exists(carDirectory)) {
                throw new DirectoryNotFoundException(carDirectory);
            }

            return new DataWrapper(carDirectory);
        }
    }
}
