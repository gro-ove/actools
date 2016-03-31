using AcManager.Tools.Managers;

namespace AcManager.Tools.AcManagersNew {
    public interface IFileAcManager : IAcManagerNew {
        AcObjectTypeDirectories Directories { get; }

        void Toggle(string id);

        void Delete(string id);

        string PrepareForAdditionalContent(string id, bool removeExisting);
    }
}