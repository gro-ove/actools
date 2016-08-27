using System;
using System.IO;
using System.Threading.Tasks;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ContentInstallation {
    public static class ContentInstallation {
        public static Task<IAdditionalContentInstallator> FromFile(string filename) {
            return FileUtils.IsDirectory(filename) ? DirectoryContentInstallator.Create(filename) :
                    IsZipArchive(filename) ? ZipContentInstallator.Create(filename) :
                            SharpCompressContentInstallator.Create(filename);
        }

        private const int ZipLeadBytes = 0x04034b50;

        private static bool IsZipArchive(string filename) {
            try {
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var bytes = new byte[4];
                    fs.Read(bytes, 0, 4);
                    return BitConverter.ToInt32(bytes, 0) == ZipLeadBytes;
                }
            } catch (Exception e) {
                Logging.Warning("IsZipArchive(): " + e);
                return false;
            }
        }
    }
}
