using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataReadWrapper {
        bool IsEmpty { get; }

        bool IsPacked { get; }

        [NotNull]
        T GetFile<T>([NotNull] string name) where T : IDataFile, new();

        [CanBeNull]
        string GetData([NotNull] string name);
    }
}