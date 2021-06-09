using System;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public abstract class OneDriveUploaderUploader : OneDriveUploaderUploaderBase {
        public OneDriveUploaderUploader(IStorage storage) : base(storage, "Microsoft OneDrive",
                "Offers 5 GB of space by default. Situation with downloading isn’t the best, but it’s better than Google Drive. This version requires an access to all files, but allows to select destination.",
                true, true) {
            Scopes = new[] {
                @"offline_access",
                @"files.readwrite"
            };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetOneDriveCredentials();
        }
    }
}