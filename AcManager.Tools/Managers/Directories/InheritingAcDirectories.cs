using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    public class InheritingAcDirectories : AcDirectoriesBase, IDirectoryListener {
        private readonly IAcDirectories _parentDirectories;

        public InheritingAcDirectories(IAcDirectories parentDirectories, [NotNull] string enabledDirectory, [CanBeNull] string disabledDirectory)
                : base(enabledDirectory, disabledDirectory) {
            _parentDirectories = parentDirectories;
        }

        public InheritingAcDirectories(IAcDirectories parentDirectories, [NotNull] string enabledDirectory)
                : base(enabledDirectory) {
            _parentDirectories = parentDirectories;
        }

        private List<IDirectoryListener> _subscribed;

        public override void Subscribe(IDirectoryListener listener) {
            if (_subscribed == null) {
                _parentDirectories.Subscribe(this);
                _subscribed = new List<IDirectoryListener>(1);
            }

            _subscribed.Add(listener);
        }

        public override void Dispose() {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TestArgs(FileSystemEventArgs e) {
            return FileUtils.IsAffected(EnabledDirectory, e.FullPath) ||
                   DisabledDirectory != null && FileUtils.IsAffected(DisabledDirectory, e.FullPath);
        }

        public void FileOrDirectoryChanged(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryChanged(sender, e);
            }
        }

        public void FileOrDirectoryCreated(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryCreated(sender, e);
            }
        }

        public void FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryDeleted(sender, e);
            }
        }

        public void FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryRenamed(sender, e);
            }
        }
    }
}