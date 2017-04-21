using System.Threading.Tasks;
using AcManager.Tools.Managers.Directories;

namespace AcManager.Tools.AcManagersNew {
    public interface IFileAcManager : IAcManagerNew {
        IAcDirectories Directories { get; }

        Task ToggleAsync(string id);

        Task RenameAsync(string oldId, string newId, bool newEnabledState);

        Task DeleteAsync(string id);

        Task CloneAsync(string id, string newId, bool newEnabled);

        Task<string> PrepareForAdditionalContentAsync(string id, bool removeExisting);
    }
}