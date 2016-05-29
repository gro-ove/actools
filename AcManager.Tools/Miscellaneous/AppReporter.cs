using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public class AppReporter {
        public static void SendLogs(string message = null) {
            var tempFilename = FilesStorage.Instance.GetTemporaryFilename("Logs.zip");
            var logsDirectory = FilesStorage.Instance.GetDirectory("Logs");
            using (var zip = ZipFile.Open(tempFilename, ZipArchiveMode.Create)) {
                if (!string.IsNullOrWhiteSpace(message)) {
                    try {
                        zip.CreateEntryFromString("Message.txt", message);
                    } catch (Exception e) {
                        Logging.Warning("Can't attach Message.txt: " + e);
                    }
                }

                try {
                    zip.CreateEntryFromFile(FileUtils.GetLogFilename(), "AC Log.txt");
                } catch (Exception e) {
                    Logging.Warning("Can't attach AC Log.txt: " + e);
                }

                try {
                    zip.CreateEntryFromString("Description.txt", JsonConvert.SerializeObject(new {
                        BuildInformation.AppVersion,
                        MainExecutingFile.Location,
                        Environment.OSVersion,
                        Environment.CommandLine,
                        Environment = Environment.GetEnvironmentVariables(),
                        SteamId = SteamIdHelper.Instance.Value
                    }, Formatting.Indented));
                } catch (Exception e) {
                    Logging.Warning("Can't attach Description.txt: " + e);
                }

                try {
                    zip.CreateEntryFromString("Values.txt", ValuesStorage.Instance.GetData());
                } catch (Exception e) {
                    Logging.Warning("Can't attach Values.data: " + e);
                }

                try {
                    if (File.Exists(FilesStorage.Instance.GetFilename("Arguments.txt"))) {
                        zip.CreateEntryFromFile(FilesStorage.Instance.GetFilename("Arguments.txt"), "Arguments.txt");
                    }
                } catch (Exception e) {
                    Logging.Warning("Can't attach Arguments.txt: " + e);
                }

                foreach (var fileInfo in new DirectoryInfo(logsDirectory).GetFiles("*.txt").Where(x => x.Length < 100000)
                        .OrderBy(x => -x.CreationTime.ToUnixTimestamp()).Take(35)) {
                    try {
                        zip.CreateEntryFromFile(fileInfo.FullName, "Logs/" + fileInfo.Name, CompressionLevel.Optimal);
                    } catch (Exception e) {
                        Logging.Warning("Can't attach Logs/" + fileInfo.Name + ": " + e);
                    }
                }
            }

            var zipBytes = File.ReadAllBytes(tempFilename);
            File.Delete(tempFilename);

            InternalUtils.SendAppReport(zipBytes, CmApiProvider.UserAgent);
        }
    }
}
