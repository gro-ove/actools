using System;
using System.Collections.Generic;
using AcManager.Tools.Managers.InnerHelpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    public interface IAcDirectories : IDisposable {
        [NotNull]
        string EnabledDirectory { get; }

        [CanBeNull]
        string DisabledDirectory { get; }

        bool Actual { get; }

        IEnumerable<string> GetSubDirectories();

        IEnumerable<string> GetSubDirectories(string searchPattern);

        IEnumerable<string> GetSubFiles(string searchPattern);

        string GetLocation([NotNull] string fileName, bool enabled);

        bool CheckIfEnabled([NotNull] string location);

        void Subscribe(IDirectoryListener listener);
    }
}