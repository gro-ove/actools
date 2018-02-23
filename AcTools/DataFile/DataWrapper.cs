using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AcTools.AcdFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class DataWrapper : DataWrapperBase, INotifyPropertyChanged {
        public static readonly string PackedFileExtension = ".acd";
        public static readonly string PackedFileName = "data.acd";
        public static readonly string UnpackedDirectoryName = "data";

        [CanBeNull]
        private Acd _acd;

        [NotNull]
        public string ParentDirectory { get; }

        public override string Location => ParentDirectory;

        private DataWrapper([NotNull] string carDirectory) {
            ParentDirectory = carDirectory;

            var dataAcd = Path.Combine(carDirectory, PackedFileName);
            if (File.Exists(dataAcd)) {
                _acd = Acd.FromFile(dataAcd);
                IsPacked = true;
            } else {
                var dataDirectory = Path.Combine(carDirectory, UnpackedDirectoryName);
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
            return FromCarDirectory(AcPaths.GetCarDirectory(acRoot, carId));
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
