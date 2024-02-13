using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.Managers.Directories;

namespace AcManager.Tools.AcManagersNew {
    public interface IFileAcManager : IAcManagerNew {
        IAcDirectories Directories { get; }
        Task ToggleAsync(string id, bool? enabled = null);
        Task ToggleAsync(IEnumerable<string> ids, bool? enabled = null);
        Task DeleteAsync(string id);
        Task DeleteAsync(IEnumerable<string> ids);
        Task RenameAsync(string oldId, string newId, bool newEnabledState);
        Task CloneAsync(string id, string newId, bool newEnabled);
        Task<string> PrepareForAdditionalContentAsync(string id, bool removeExisting);
    }
}