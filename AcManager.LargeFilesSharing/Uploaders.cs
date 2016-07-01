using AcManager.LargeFilesSharing.GoogleDrive;

namespace AcManager.LargeFilesSharing {
    public class Uploaders {
        public static ILargeFileUploader[] List { get; } = {
            new GoogleDriveUploader()
        };
    }
}