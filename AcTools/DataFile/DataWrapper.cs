using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AcTools.AcdFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataWrapper {
        [NotNull]
        string Location { get; }

        [NotNull]
        T GetFile<T>([NotNull] string name) where T : IDataFile, new();

        [CanBeNull]
        string GetData([NotNull] string name);

        bool Contains([NotNull] string name);
        void Refresh([CanBeNull] string name);
        void SetData([NotNull] string name, [CanBeNull] string data, bool recycleOriginal = false);
        void Delete([NotNull] string name, bool recycleOriginal = false);
    }

    public static class DataWrapperExtension {
        public static IniFile GetIniFile(this IDataWrapper data, string name) {
            return data.GetFile<IniFile>(name);
        }

        public static LutDataFile GetLutFile(this IDataWrapper data, string name) {
            return data.GetFile<LutDataFile>(name);
        }

        public static RtoDataFile GetRtoFile(this IDataWrapper data, string name) {
            return data.GetFile<RtoDataFile>(name);
        }

        public static RawDataFile GetRawFile(this IDataWrapper data, string name) {
            return data.GetFile<RawDataFile>(name);
        }
    }

    /*public interface IDataWrapper {
        IniFile GetIniFile([Localizable(false)] string name);

        LutDataFile GetLutFile(string name);

        RtoDataFile GetRtoFile(string name);

        RawDataFile GetRawFile(string name);
    }*/

    public abstract class DataWrapperBase : IDataWrapper {
        private object _cacheLock = new object();

        [CanBeNull]
        private Dictionary<string, IDataFile> _cache;

        public abstract string Location { get; }

        public T GetFile<T>(string name) where T : IDataFile, new() {
            lock (_cacheLock) {
                if (_cache == null) {
                    _cache = new Dictionary<string, IDataFile>();
                }

                if (_cache.TryGetValue(name, out var v) && v is T) {
                    return (T)v;
                }
            }

            var t = new T();
            lock (_cacheLock) {
                _cache[name] = t;
            }

            InitializeFile(t, name);
            return t;
        }

        protected virtual void InitializeFile(IDataFile dataFile, string name) {
            dataFile.Initialize(this, name, null);
        }

        public abstract string GetData(string name);
        public abstract bool Contains(string name);

        protected void ClearCache() {
            if (_cache == null) return;
            lock (_cacheLock) {
                _cache.Clear();
            }
        }

        public void Refresh(string name) {
            if (_cache != null) {
                lock (_cacheLock) {
                    if (name == null) {
                        _cache.Clear();
                    } else if (_cache.ContainsKey(name)) {
                        _cache.Remove(name);
                    }
                }
            }

            RefreshOverride(name);
        }

        public void SetData(string name, string data, bool recycleOriginal = false) {
            if (_cache != null) {
                lock (_cacheLock) {
                    _cache.Remove(name);
                }
            }

            SetDataOverride(name, data, recycleOriginal);
        }

        public void Delete(string name, bool recycleOriginal = false) {
            if (_cache != null) {
                lock (_cacheLock) {
                    _cache.Remove(name);
                }
            }

            DeleteOverride(name, recycleOriginal);
        }

        protected abstract void RefreshOverride(string name);
        protected abstract void SetDataOverride(string name, string data, bool recycleOriginal);
        protected abstract void DeleteOverride(string name, bool recycleOriginal);
    }

    public class DataDirectoryWrapper : DataWrapperBase {
        private readonly string _directory;

        public DataDirectoryWrapper(string directory) {
            if (!Directory.Exists(directory)) {
                throw new DirectoryNotFoundException(directory);
            }

            _directory = directory;
        }

        public override string Location => _directory;

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

    public class DataWrapper : DataWrapperBase, INotifyPropertyChanged {
        [CanBeNull]
        private Acd _acd;

        [NotNull]
        public string ParentDirectory { get; }

        public override string Location => ParentDirectory;

        private DataWrapper([NotNull] string carDirectory) {
            ParentDirectory = carDirectory;

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

        public override string GetData(string name) {
            return _acd?.GetEntry(name)?.ToString();
        }

        protected override void InitializeFile(IDataFile dataFile, string name) {
            if (_acd?.IsPacked == false) {
                dataFile.Initialize(this, name, _acd.GetFilename(name));
            } else {
                base.InitializeFile(dataFile, name);
            }
        }

        public override bool Contains(string name) {
            return !IsEmpty && _acd?.GetEntry(name) != null;
        }

        protected override void RefreshOverride(string name) {
            var dataAcd = Path.Combine(ParentDirectory, "data.acd");
            if (File.Exists(dataAcd)) {
                if (!IsPacked) {
                    ClearCache();
                }

                _acd = Acd.FromFile(dataAcd);
                IsPacked = true;
                IsEmpty = false;
            } else {
                if (IsPacked) {
                    ClearCache();
                }

                IsPacked = false;

                var dataDirectory = Path.Combine(ParentDirectory, "data");
                if (Directory.Exists(dataDirectory)) {
                    _acd = Acd.FromDirectory(dataDirectory);
                    IsEmpty = false;
                } else {
                    IsEmpty = true;
                }
            }

            OnDataChanged(name);
        }

        protected override void SetDataOverride(string name, string data, bool recycleOriginal = false) {
            var acd = _acd;
            if (acd == null) return;
            acd.SetEntry(name, data);
            acd.Update(recycleOriginal);
        }

        protected override void DeleteOverride(string name, bool recycleOriginal = false) {
            var acd = _acd;
            if (acd == null) return;
            acd.DeleteEntry(name);
            acd.Update(recycleOriginal);
        }

        private bool _isPacked;

        public bool IsPacked {
            get => _isPacked;
            private set {
                if (value == _isPacked) return;
                _isPacked = value;
                OnPropertyChanged();
            }
        }

        private bool _isEmpty;

        public bool IsEmpty {
            get => _isEmpty;
            private set {
                if (value == _isEmpty) return;
                _isEmpty = value;
                OnPropertyChanged();
            }
        }

        [NotNull]
        public static DataWrapper FromCarDirectory([NotNull] string carDirectory) {
            if (!Directory.Exists(carDirectory)) {
                throw new DirectoryNotFoundException(carDirectory);
            }

            return new DataWrapper(carDirectory);
        }

        [NotNull]
        public static DataWrapper FromCarDirectory([NotNull] string acRoot, [NotNull] string carId) {
            return FromCarDirectory(FileUtils.GetCarDirectory(acRoot, carId));
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
