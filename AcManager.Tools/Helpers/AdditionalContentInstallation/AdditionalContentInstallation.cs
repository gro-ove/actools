using System;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class AdditionalContentInstallation {
        public static Task<IAdditionalContentInstallator> FromFile(string filename) {
            return ArchiveContentInstallator.Create(filename);
        }
    }
}
