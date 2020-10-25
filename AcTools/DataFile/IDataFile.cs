using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataFile {
        [NotNull]
        string Name { get; }

        [CanBeNull]
        string Filename { get; }

        void Initialize([CanBeNull] IDataReadWrapper data, [NotNull] string name, [CanBeNull] string filename);
    }
}