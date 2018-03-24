using System;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public class OneDriveUploaderAppFolderUploader : OneDriveUploaderUploaderBase {
        public OneDriveUploaderAppFolderUploader(IStorage storage) : base(storage, "Microsoft OneDrive (App Folder)",
                "Offers 5 GB of space by default. Situation with downloading isn’t the best, but it’s better than Google Drive. This version only accesses app’s folder.",
                true, true) {
            Scopes = new[] {
                @"offline_access",
                @"files.readwrite.appfolder"
            };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetOneDriveAppFolderCredentials();
        }
    }
}