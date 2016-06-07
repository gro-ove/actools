using AcManager.Tools.Managers.Directories;

namespace AcManager.Tools.AcManagersNew {
    public interface IFileAcManager : IAcManagerNew {
        IAcDirectories Directories { get; }

        void Toggle(string id);

        void Rename(string id, string newFileName, bool newEnabledState);

        void Delete(string id);

        string PrepareForAdditionalContent(string id, bool removeExisting);
    }
}