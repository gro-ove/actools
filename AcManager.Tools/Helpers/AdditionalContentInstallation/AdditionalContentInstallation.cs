using SevenZip;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class AdditionalContentInstallation {
        public static IAdditionalContentInstallator FromFile(string filename) {
            return new ArchiveContentInstallator(filename);
        }

        public static void Initialize(string sevenZipDllPath) {
            SevenZipBase.SetLibraryPath(sevenZipDllPath);
        }
    }
}
