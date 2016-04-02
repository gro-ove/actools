using AcManager.Tools.Managers.Directories;

namespace AcManager.Tools.AcManagersNew {
    public interface IFileAcManager : IAcManagerNew {
        BaseAcDirectories Directories { get; }

        void Toggle(string id);

        void Delete(string id);

        string PrepareForAdditionalContent(string id, bool removeExisting);
    }
}