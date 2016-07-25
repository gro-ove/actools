using System;
using AcManager.Tools.ContentInstallation.Types;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentEntry {
        [NotNull]
        public string Id { get; }

        /// <summary>
        /// Empty if object’s in root.
        /// </summary>
        [NotNull]
        public string Path { get; }

        public ContentType Type { get; }

        [NotNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        public ContentEntry(ContentType type, [NotNull] string path, [NotNull] string id, string name = null, string version = null) {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (id == null) throw new ArgumentNullException(nameof(id));

            Type = type;
            Path = path;
            Id = id;
            Name = name ?? id;
            Version = version;
        }
    }
}
