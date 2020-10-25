using JetBrains.Annotations;

namespace AcTools.DataFile {
    public interface IDataWrapper : IDataReadWrapper {
        [CanBeNull]
        string Location { get; }

        bool Contains([NotNull] string name);
        void Refresh([CanBeNull] string name);
        void SetData([NotNull] string name, [CanBeNull] string data, bool recycleOriginal = false);
        void Delete([NotNull] string name, bool recycleOriginal = false);
    }
}