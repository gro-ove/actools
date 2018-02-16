using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public abstract class FileUploaderBase : NotifyPropertyChanged, ILargeFileUploader {
        protected readonly IStorage Storage;
        internal readonly Request Request = new Request();

        private const string KeyDestinationDirectoryId = "directoryId";

        protected FileUploaderBase(IStorage storage, string name, [CanBeNull] Uri icon, string description, bool supportsSigning, bool supportsDirectories) {
            Id = GetType().Name;
            Storage = new Substorage(storage, Id + ":");
            _destinationDirectoryId = Storage.Get<string>(KeyDestinationDirectoryId);

            DisplayName = name;
            Description = description;
            Icon = icon;
            SupportsSigning = supportsSigning;
            SupportsDirectories = supportsDirectories;
        }

        public string Id { get; }
        public bool SupportsSigning { get; }
        public bool SupportsDirectories { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public Uri Icon { get; }

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
            protected set => Apply(value, ref _isReady);
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
            if (url == null) {
                RaiseShareFailedException();
            }

            return new UploadResult {
                Id = $"{(uploadAs == UploadAs.Content ? "I6" : "Ii")}{url.ToCutBase64()}",
                DirectUrl = url
            };
        }
    }
}