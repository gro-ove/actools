using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using AcManager.Internal;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Win32;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Writer;

namespace AcManager.Tools.Miscellaneous {
    public class AppReporter {
        private static string GetUserName() {
            return SettingsHolder.Drive.DifferentPlayerNameOnline ? $"{SettingsHolder.Drive.PlayerName} ({ SettingsHolder.Drive.PlayerNameOnline})"
                    : SettingsHolder.Drive.PlayerName;
        }

        private static string GetWindowsName() {
            return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")?.GetValue("ProductName") as string ??
                    WindowsVersionHelper.GetVersion();
        }

        [Localizable(false)]
        public static void SendUnpackedLocale(string directory, string message = null) {
            using (var memory = new MemoryStream()) {
                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                    if (!string.IsNullOrWhiteSpace(message)) {
                        try {
                            writer.WriteString("Message.txt", message);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Message.txt: " + e);
                        }
                    }

                    try {
                        writer.WriteString("Description.txt", JsonConvert.SerializeObject(new {
                            BuildInformation.AppVersion,
                            MainExecutingFile.Location,
                            Environment.OSVersion,
                            Environment.CommandLine,
                            Environment = Environment.GetEnvironmentVariables(),
                            SteamId = SteamIdHelper.Instance.Value
                        }, Formatting.Indented));
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach Description.txt: " + e);
                    }

                    foreach (var fileInfo in new DirectoryInfo(directory).GetFiles("*.resx")) {
                        try {
                            writer.Write("Locale/" + fileInfo.Name, fileInfo.FullName);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Locale/" + fileInfo.Name + ": " + e);
                        }
                    }

                    var data = memory.ToArray();
                    if (data.Length > 20000000) {
                        throw new Exception("Size limit exceeded");
                    }
                }

                File.WriteAllBytes(Path.Combine(directory, "sent.zip"), memory.ToArray());
                InternalUtils.SendLocale(memory.ToArray(), $@"Name: {GetUserName()}
Used language: {CultureInfo.CurrentUICulture.Name}
Locale: {Path.GetFileName(directory)}
Operating system: {GetWindowsName()}
App version: {BuildInformation.AppVersion}", CmApiProvider.UserAgent);
            }
        }

        [Localizable(false)]
        public static void SendLogs(string message = null) {
            var logsDirectory = FilesStorage.Instance.GetDirectory("Logs");

            using (var memory = new MemoryStream()) {
                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                    if (!string.IsNullOrWhiteSpace(message)) {
                        try {
                            writer.WriteString("Message.txt", message);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Message.txt: " + e);
                        }
                    }

                    try {
                        writer.Write("AC Log.txt", FileUtils.GetLogFilename());
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach AC Log.txt: " + e);
                    }

                    try {
                        writer.WriteString("Description.txt", JsonConvert.SerializeObject(new {
                            BuildInformation.AppVersion,
                            MainExecutingFile.Location,
                            Environment.OSVersion,
                            Environment.CommandLine,
                            Environment = Environment.GetEnvironmentVariables(),
                            SteamId = SteamIdHelper.Instance.Value
                        }, Formatting.Indented));
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach Description.txt: " + e);
                    }

                    try {
                        writer.WriteString("Values.txt", ValuesStorage.Storage.GetData());
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach Values.data: " + e);
                    }

                    try {
                        if (File.Exists(FilesStorage.Instance.GetFilename("Arguments.txt"))) {
                            writer.Write("Arguments.txt", FilesStorage.Instance.GetFilename("Arguments.txt"));
                        }
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach Arguments.txt: " + e);
                    }

                    foreach (var fileInfo in new DirectoryInfo(FileUtils.GetDocumentsCfgDirectory()).GetFiles("*.ini").Where(x => x.Length < 500000).Take(100)) {
                        try {
                            writer.Write("Config/" + fileInfo.Name, fileInfo.FullName);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Config/" + fileInfo.Name + ": " + e);
                        }
                    }

                    if (AcRootDirectory.Instance.Value != null) {
                        foreach (var fileInfo in new DirectoryInfo(FileUtils.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue)).GetFiles("*.ini")
                                                                                                                                 .Where(x => x.Length < 500000)
                                                                                                                                 .Take(100)) {
                            try {
                                writer.Write("SysConfig/" + fileInfo.Name, fileInfo.FullName);
                            } catch (Exception e) {
                                Logging.Warning("Can’t attach SysConfig/" + fileInfo.Name + ": " + e);
                            }
                        }
                    }

                    var raceOut = FileUtils.GetResultJsonFilename();
                    if (File.Exists(raceOut)) {
                        try {
                            writer.Write("Race.json", raceOut);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Race.json:" + e);
                        }
                    }

                    var career = FileUtils.GetKunosCareerProgressFilename(); ;
                    if (File.Exists(career)) {
                        try {
                            writer.Write("Career.ini", career);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Career.ini:" + e);
                        }
                    }

                    foreach (var fileInfo in new DirectoryInfo(logsDirectory).GetFiles("*.log").Where(x => x.Length < 2000000).TakeLast(35)) {
                        try {
                            writer.Write("Logs/" + fileInfo.Name, fileInfo.FullName);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Logs/" + fileInfo.Name + ": " + e);
                        }
                    }
                }

                var data = memory.ToArray();
                if (data.Length > 20000000) {
                    File.WriteAllBytes(FilesStorage.Instance.GetTemporaryFilename("Report.zip"), data);
                    throw new Exception("Size limit exceeded");
                }

                InternalUtils.SendAppReport(memory.ToArray(), $@"Name: {GetUserName()}
Operating system: {GetWindowsName()}
App version: {BuildInformation.AppVersion}", CmApiProvider.UserAgent);
            }
        }

        [Localizable(false)]
        public static void SendData([NotNull] string name, [NotNull] object obj) {
            using (var memory = new MemoryStream()) {
                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                    try {
                        writer.WriteString(name, JsonConvert.SerializeObject(obj));
                    } catch (Exception e) {
                        Logging.Warning($"Can’t attach {obj}: " + e);
                    }
                }

                var data = memory.ToArray();
                if (data.Length > 20000000) {
                    File.WriteAllBytes(FilesStorage.Instance.GetTemporaryFilename("Data.zip"), data);
                    throw new Exception("Size limit exceeded");
                }

                InternalUtils.SendData(memory.ToArray(), $@"Name: {GetUserName()}
Operating system: {GetWindowsName()}
App version: {BuildInformation.AppVersion}", CmApiProvider.UserAgent);
            }
        }
    }
}
