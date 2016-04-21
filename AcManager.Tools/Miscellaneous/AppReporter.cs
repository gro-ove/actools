using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public partial class AppReporter {
        private static void Send(string url, byte[] data) {
            if (ServerAddress == null) return;

            try {
                using (var client = new WebClient { Headers = {
                    [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent,
                    [CmApiProvider.ChecksumHeader] = GetChecksum(data)
                } }) {
                    client.UploadData(ServerAddress + url, data);
                }

                Logging.Write($"[APPREPORTER] Sent {data.Length} byte(s)");
            } catch (Exception e) {
                Logging.Warning($"[APPREPORTER] Cannot upload {url}: " + e);
                throw;
            }
        }

        private static string GetChecksum(byte[] data) {
            if (ChecksumSalt == null) return null;
            using (var sha1 = SHA1.Create()) {
                return sha1.ComputeHash(
                    Encoding.UTF8.GetBytes(sha1.ComputeHash(data).ToHexString() + ChecksumSalt)
                ).ToHexString();
            }
        }

        public static void SendLogs(string message = null) {
            if (ChecksumSalt == null || EncryptionKey == null || ServerAddress == null) {
                NonfatalError.Notify("Can't send logs", "Server parameters are missing.");
                return;
            }

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

            var encryptedBytes = StringCipher.Encrypt(zipBytes, EncryptionKey);
            Send("logs", encryptedBytes);
        }
    }
}
