using System;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public class GoogleDriveAppFolderUploader : GoogleDriveUploaderBase {
        public GoogleDriveAppFolderUploader(IStorage storage) : base(storage, "Google Drive (Upload To Root)",
                "15 GB of space, but sadly without any API to download shared files, so CM might break any moment. This version do not require an access to all files, but uploads files only to root directory.",
                true, false) {
            Scopes = new[] { @"https://www.googleapis.com/auth/drive.file" };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetGoogleDriveAppFolderCredentials();
        }
    }
}