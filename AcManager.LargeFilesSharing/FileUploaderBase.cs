using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public abstract class FileUploaderBase : ILargeFileUploader {
        protected readonly IStorage Storage;

        protected FileUploaderBase(IStorage storage, string name, bool supportsSigning, bool supportsDirectories) {
            Storage = storage;

            Id = GetType().Name;
            _keyDestinationDirectoryId = @"fub.ddi." + Id;
            _destinationDirectoryId = Storage.GetString(_keyDestinationDirectoryId);

            DisplayName = name;
            SupportsSigning = supportsSigning;
            SupportsDirectories = supportsDirectories;
        }

        public string Id { get; }
        public bool SupportsSigning { get; }
        public bool SupportsDirectories { get; }
        public string DisplayName { get; }

        private readonly string _keyDestinationDirectoryId;

        private string _destinationDirectoryId;

        public string DestinationDirectoryId {
            get => _destinationDirectoryId;
            set {
                if (Equals(value, _destinationDirectoryId)) return;
                _destinationDirectoryId = value;
                OnPropertyChanged();
                Storage.Set(_keyDestinationDirectoryId, value);
            }
        }

        private bool _isReady;

        public bool IsReady {
            get => _isReady;
            protected set {
                if (Equals(value, _isReady)) return;
                _isReady = value;
                OnPropertyChanged();
            }
        }

        public virtual Task Reset() {
            IsReady = false;
            return Task.Delay(0);
        }

        public abstract Task Prepare(CancellationToken cancellation);

        public abstract Task SignIn(CancellationToken cancellation);

        public abstract Task<DirectoryEntry[]> GetDirectories(CancellationToken cancellation);

        public abstract Task<UploadResult> Upload(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        protected static string GetMimeType(string filename) {
            var ext = Path.GetExtension(filename)?.ToLower();
            if (ext != null) {
                var regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
                if (regKey?.GetValue("Content Type") != null) {
                    return regKey.GetValue("Content Type").ToString();
                }
            }

            return @"application/unknown";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}