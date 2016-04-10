using System.Threading.Tasks;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class AdditionalContentInstallation {
        public static Task<IAdditionalContentInstallator> FromFile(string filename) {
            return FileUtils.IsDirectory(filename) ? DirectoryContentInstallator.Create(filename) : ArchiveContentInstallator.Create(filename);
        }
    }
}
