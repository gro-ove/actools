using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.ContentInstallation {
    public static class ContentInstallation {
        public static Task<IAdditionalContentInstallator> FromFile(string filename, ContentInstallationParams installationParams,
                CancellationToken cancellation) {
            if (FileUtils.IsDirectory(filename)) {
                return DirectoryContentInstallator.Create(filename, installationParams, cancellation);
            }

            if (!IsZipArchive(filename) && PluginsManager.Instance.GetById(SevenZipContentInstallator.PluginId)?.IsReady == true) {
                try {
                    return SevenZipContentInstallator.Create(filename, installationParams, cancellation);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t use 7-Zip to unpack", e);
                }
            }

            return SharpCompressContentInstallator.Create(filename, installationParams, cancellation);
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
                Logging.Warning(e);
                return false;
            }
        }
    }
}
