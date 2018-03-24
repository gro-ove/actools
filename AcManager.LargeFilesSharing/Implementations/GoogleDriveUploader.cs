using System;
using AcManager.Internal;
using AcManager.Tools;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public class GoogleDriveUploader : GoogleDriveUploaderBase {
        public GoogleDriveUploader(IStorage storage) : base(storage, ToolsStrings.Uploader_GoogleDrive,
                "15 GB of space, but sadly without any API to download shared files, so CM might break any moment. This version requires an access to all files, but allows to select destination.",
                true, true) {
            Scopes = new[] { @"https://www.googleapis.com/auth/drive", @"https://www.googleapis.com/auth/drive.file" };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetGoogleDriveCredentials();
        }
    }
}