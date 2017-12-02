using JetBrains.Annotations;

namespace AcTools.GenericMods {
    public class GenericModFile {
        [CanBeNull]
        public readonly string Source;

        [NotNull]
        public readonly string Destination;

        [NotNull]
        public readonly string Backup;

        [NotNull]
        public readonly string RelativeName;

        [NotNull]
        public readonly string ModName;

        public GenericModFile([CanBeNull] string source, [NotNull] string destination, [NotNull] string backup,
                [NotNull] string relativeName, [NotNull] string modName) {
            RelativeName = relativeName;
            Source = source;
            Destination = destination;
            Backup = backup;
            ModName = modName;
        }
    }
}