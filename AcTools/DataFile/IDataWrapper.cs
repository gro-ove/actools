using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataWrapper {
        [NotNull]
        string Location { get; }

        [NotNull]
        T GetFile<T>([NotNull] string name) where T : IDataFile, new();

        [CanBeNull]
        string GetData([NotNull] string name);

        bool Contains([NotNull] string name);
        void Refresh([CanBeNull] string name);
        void SetData([NotNull] string name, [CanBeNull] string data, bool recycleOriginal = false);
        void Delete([NotNull] string name, bool recycleOriginal = false);
    }
}