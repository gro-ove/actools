using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public abstract class FileUploaderBase : ILargeFileUploader {
        protected readonly IStorage Storage;

        private const string KeyDestinationDirectoryId = "directoryId";

        protected FileUploaderBase(IStorage storage, string name, string description, bool supportsSigning, bool supportsDirectories) {
            Id = GetType().Name;
            Storage = storage.GetSubstorage(Id + ":");
            _destinationDirectoryId = Storage.GetString(KeyDestinationDirectoryId);

            DisplayName = name;
            Description = description;
            SupportsSigning = supportsSigning;
            SupportsDirectories = supportsDirectories;
        }

        public string Id { get; }
        public bool SupportsSigning { get; }
        public bool SupportsDirectories { get; }
        public string DisplayName { get; }
        public string Description { get; }

        private string _destinationDirectoryId;

        public string DestinationDirectoryId {
            get => _destinationDirectoryId;
            set {
                if (Equals(value, _destinationDirectoryId)) return;
                _destinationDirectoryId = value;
                OnPropertyChanged();
                Storage.Set(KeyDestinationDirectoryId, value);
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

        public virtual Task ResetAsync(CancellationToken cancellation) {
            IsReady = false;
            return Task.Delay(0);
        }

        public abstract Task PrepareAsync(CancellationToken cancellation);

        public abstract Task SignInAsync(CancellationToken cancellation);

        public abstract Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation);

        public abstract Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
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

        [ContractAnnotation("=> halt")]
        public void RaiseUploadFailedException(string reason = null) {
            throw new InformativeException(ToolsStrings.Uploader_CannotUploadToGoogleDrive.Replace("Google Drive", DisplayName),
                    reason ?? ToolsStrings.Common_MakeSureThereIsEnoughSpace);
        }

        [ContractAnnotation("=> halt")]
        public void RaiseShareFailedException(string reason = null) {
            throw new InformativeException(ToolsStrings.Uploader_CannotShareGoogleDrive.Replace("Google Drive", DisplayName), reason);
        }

        protected UploadResult WrapUrl(string url, UploadAs uploadAs) {
            return new UploadResult {
                Id = $"{(uploadAs == UploadAs.Content ? "I6" : "Ii")}{Convert.ToBase64String(Encoding.UTF8.GetBytes(url))}",
                DirectUrl = url
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}