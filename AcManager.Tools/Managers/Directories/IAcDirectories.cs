using System;
using System.Collections.Generic;
using System.IO;
using AcManager.Tools.Managers.InnerHelpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Directories {
    public interface IAcDirectories : IDisposable {
        bool Actual { get; }

        bool CheckIfEnabled([NotNull] string location);

        /// <summary>
        /// For internal use in Extension.
        /// </summary>
        [NotNull, ItemNotNull]
        IEnumerable<string> GetContent([NotNull] Func<string, string[]> contentFromDirectoriesSelector);

        [NotNull]
        string GetId([NotNull] string location);

        [NotNull]
        string GetLocation([NotNull] string id, bool enabled);

        [CanBeNull]
        string GetLocationByFilename([NotNull] string filename, out bool inner);

        [NotNull]
        string GetUniqueId([NotNull] string preferredId,
                [NotNull] string postfix = "-{0}", bool forcePostfix = false, int startFrom = 1);

        void Subscribe(IDirectoryListener listener);
    }

    public static class AcDirectoriesExtension {
        public static string GetMainDirectory(this IAcDirectories directories) {
            return Path.GetDirectoryName(directories.GetLocation(@"_", true));
        }

        public static IEnumerable<string> GetContentDirectories(this IAcDirectories directories) {
            return directories.GetContent(Directory.GetDirectories);
        }

        public static IEnumerable<string> GetContentDirectories(this IAcDirectories directories, string searchPattern) {
            return directories.GetContent(x => Directory.GetDirectories(x, searchPattern));
        }

        public static IEnumerable<string> GetContentFiles(this IAcDirectories directories, string searchPattern) {
            return directories.GetContent(x => Directory.GetFiles(x, searchPattern));
        }
    }
}