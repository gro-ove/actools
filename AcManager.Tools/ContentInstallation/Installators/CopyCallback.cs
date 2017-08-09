using System;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    /// <summary>
    /// Takes file information and, if copy needed, returns destination path.
    /// </summary>
    public interface ICopyCallback {
        [CanBeNull]
        string File ([NotNull] IFileInfo info);

        [CanBeNull]
        string Directory ([NotNull] IDirectoryInfo info);
    }

    public class CopyCallback : ICopyCallback {
        private Func<IFileInfo, string> _file;
        private Func<IDirectoryInfo, string> _directory;

        public CopyCallback(Func<IFileInfo, string> file, Func<IDirectoryInfo, string> directory = null) {
            _file = file;
            _directory = directory;
        }

        public string File(IFileInfo info) {
            return _file?.Invoke(info);
        }

        public string Directory(IDirectoryInfo info) {
            return _directory?.Invoke(info);
        }
    }
}