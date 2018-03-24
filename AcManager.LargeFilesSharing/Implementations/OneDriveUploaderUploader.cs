using System;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public abstract class OneDriveUploaderUploader : OneDriveUploaderUploaderBase {
        public OneDriveUploaderUploader(IStorage storage) : base(storage, "Microsoft OneDrive",
                "Offers 5 GB of space by default. Situation with downloading isn’t the best, but it’s better than Google Drive. This version requires an access to all files, but allows to select destination.",
                true, true) {
            // Scopes = new[] { @"Files.ReadWrite.AppFolder" };
        }

        protected override Tuple<string, string> GetCredentials() {
            throw new NotImplementedException();
            // return InternalUtils.GetOneDriveCredentials();
        }
    }
}