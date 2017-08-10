using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataFile {
        [NotNull]
        string Name { get; }

        [CanBeNull]
        string Filename { get; }

        void Initialize([CanBeNull] IDataWrapper data, [NotNull] string name, [CanBeNull] string filename);
    }
}