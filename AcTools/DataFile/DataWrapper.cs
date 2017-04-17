using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AcTools.AcdFile;
using JetBrains.Annotations;

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

    public class DataWrapper : IDataWrapper, INotifyPropertyChanged {
        private readonly string _carDirectory;
        private readonly Dictionary<string, AbstractDataFile> _cache;

        private Acd _acd;

        public string ParentDirectory => _carDirectory ?? _acd?.ParentDirectory;

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
                } else {
                    IsEmpty = true;
                }
            }
        }

        public void Refresh([CanBeNull] string localName) {
            lock (_cache) {
                if (localName == null) {
                    _cache.Clear();
                } else if (_cache.ContainsKey(localName)) {
                    _cache.Remove(localName);
                }

                var dataAcd = Path.Combine(_carDirectory, "data.acd");
                if (File.Exists(dataAcd)) {
                    if (!IsPacked) {
                        _cache.Clear();
                    }

                    _acd = Acd.FromFile(dataAcd);
                    IsPacked = true;
                    IsEmpty = false;
                } else {
                    if (IsPacked) {
                        _cache.Clear();
                    }

                    IsPacked = false;

                    var dataDirectory = Path.Combine(_carDirectory, "data");
                    if (Directory.Exists(dataDirectory)) {
                        _acd = Acd.FromDirectory(dataDirectory);
                        IsEmpty = false;
                    } else {
                        IsEmpty = true;
                    }
                }

                OnDataChanged(localName);
            }
        }

        private bool _isPacked;

        public bool IsPacked {
            get { return _isPacked; }
            private set {
                if (value == _isPacked) return;
                _isPacked = value;
                OnPropertyChanged();
            }
        }

        private bool _isEmpty;

        public bool IsEmpty {
            get { return _isEmpty; }
            private set {
                if (value == _isEmpty) return;
                _isEmpty = value;
                OnPropertyChanged();
            }
        }

        public IniFile GetIniFile([Localizable(false)] string name) {
            lock (_cache) {
                AbstractDataFile cached;
                if (_cache.TryGetValue(name, out cached) && cached is IniFile) {
                    return (IniFile)cached;
                }

                var result = new IniFile(_carDirectory, name, _acd);
                _cache[name] = result;
                return result;
            }
        }

        public LutDataFile GetLutFile(string name) {
            lock (_cache) {
                AbstractDataFile cached;
                if (_cache.TryGetValue(name, out cached) && cached is LutDataFile) {
                    return (LutDataFile)cached;
                }

                var result = new LutDataFile(_carDirectory, name, _acd);
                _cache[name] = result;
                return result;
            }
        }

        public RawDataFile GetRawFile(string name) {
            lock (_cache) {
                AbstractDataFile cached;
                if (_cache.TryGetValue(name, out cached) && cached is RawDataFile) {
                    return (RawDataFile)cached;
                }

                var result = new RawDataFile(_carDirectory, name, _acd);
                _cache[name] = result;
                return result;
            }
        }

        [NotNull]
        public static DataWrapper FromDirectory([NotNull] string carDirectory) {
            if (!Directory.Exists(carDirectory)) {
                throw new DirectoryNotFoundException(carDirectory);
            }

            return new DataWrapper(carDirectory);
        }

        public event EventHandler<DataChangedEventArgs> DataChanged;

        private void OnDataChanged([CanBeNull] string localName) {
            DataChanged?.Invoke(this, new DataChangedEventArgs(localName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DataChangedEventArgs : EventArgs {
        public DataChangedEventArgs(string propertyName) {
            PropertyName = propertyName;
        }

        [CanBeNull]
        public string PropertyName { get; }
    }
}
