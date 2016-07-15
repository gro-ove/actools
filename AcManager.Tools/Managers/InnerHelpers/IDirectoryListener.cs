using System.IO;

namespace AcManager.Tools.Managers.InnerHelpers {
    public interface IDirectoryListener {
        void FileOrDirectoryChanged(object sender, FileSystemEventArgs e);

        void FileOrDirectoryCreated(object sender, FileSystemEventArgs e);

        void FileOrDirectoryDeleted(object sender, FileSystemEventArgs e);

        void FileOrDirectoryRenamed(object sender, RenamedEventArgs e);
    }
}
