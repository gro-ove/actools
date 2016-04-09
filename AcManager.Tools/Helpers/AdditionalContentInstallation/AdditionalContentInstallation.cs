using System.Threading.Tasks;
using SevenZip;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class AdditionalContentInstallation {
        public static Task<ArchiveContentInstallator> FromFile(string filename) {
            return ArchiveContentInstallator.Create(filename);
        }

        public static void Initialize(string sevenZipDllPath) {
            SevenZipBase.SetLibraryPath(sevenZipDllPath);
        }
    }
}
