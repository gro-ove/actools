using System;
using System.Threading.Tasks;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class AdditionalContentInstallation {
        public static Task<IAdditionalContentInstallator> FromFile(string filename) {
            return FileUtils.IsDirectory(filename) ? DirectoryContentInstallator.Create(filename) :
                    filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ZipContentInstallator.Create(filename) :
                            SharpCompressContentInstallator.Create(filename);
        }
    }
}
