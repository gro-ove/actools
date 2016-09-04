using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    internal abstract class FileUploaderBase : ILargeFileUploader {
        protected FileUploaderBase(string name, bool supportsSigning, bool supportsDirectories) {
            Id = GetType().Name;
            _keyDestinationDirectoryId = @"fub.ddi." + Id;
            _destinationDirectoryId = ValuesStorage.GetString(_keyDestinationDirectoryId);

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
            get { return _destinationDirectoryId; }
            set {
                if (Equals(value, _destinationDirectoryId)) return;
                _destinationDirectoryId = value;
                OnPropertyChanged();
                ValuesStorage.Set(_keyDestinationDirectoryId, value);
            }
        }

        private bool _isReady;

        public bool IsReady {
            get { return _isReady; }
            protected set {
                if (Equals(value, _isReady)) return;
                _isReady = value;
                OnPropertyChanged();
            }
        }

        public abstract void Reset();

        public abstract Task Prepare(CancellationToken cancellation);

        public abstract Task SignIn(CancellationToken cancellation);

        public abstract Task<DirectoryEntry[]> GetDirectories(CancellationToken cancellation);

        public abstract Task<UploadResult> Upload(string name, string originalName, string mimeType, string description, byte[] data, IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation);

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