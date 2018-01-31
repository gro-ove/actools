using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using AcManager.Internal;
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
using SharpCompress.Writers;

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
                            BuildInformation.Platform,
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
                InternalUtils.SendLocale(memory.ToArray(), $@"Name: {GetUserName()};
Used language: {CultureInfo.CurrentUICulture.Name};
Locale: {Path.GetFileName(directory)};
Operating system: {GetWindowsName()};
App version: {BuildInformation.AppVersion}.", CmApiProvider.UserAgent);
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
                        writer.Write("AC Log.txt", AcPaths.GetLogFilename());
                    } catch (Exception e) {
                        Logging.Warning("Can’t attach AC Log.txt: " + e);
                    }

                    string wpfVersion;
                    try {
                        using (var r = Registry.LocalMachine.OpenSubKey(
                                                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.0\Setup\Windows Presentation Foundation")) {
                            wpfVersion = r?.GetValue("Version")?.ToString();
                        }
                    } catch (Exception e) {
                        Logging.Warning("Can’t get WPF version: " + e);
                        wpfVersion = null;
                    }

                    try {
                        writer.WriteString("Description.txt", JsonConvert.SerializeObject(new {
                            BuildInformation.AppVersion,
                            BuildInformation.Platform,
                            MainExecutingFile.Location,
                            Environment.OSVersion,
                            Environment.CommandLine,
                            Environment = Environment.GetEnvironmentVariables(),
                            SteamId = SteamIdHelper.Instance.Value,
                            typeof(string).Assembly.ImageRuntimeVersion,
                            WpfVersion = wpfVersion
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

                    foreach (var fileInfo in new DirectoryInfo(AcPaths.GetDocumentsCfgDirectory()).GetFiles("*.ini").Where(x => x.Length < 500000).Take(100)) {
                        try {
                            writer.Write("Config/" + fileInfo.Name, fileInfo.FullName);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Config/" + fileInfo.Name + ": " + e);
                        }
                    }

                    if (AcRootDirectory.Instance.Value != null) {
                        foreach (var fileInfo in new DirectoryInfo(AcPaths.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue)).GetFiles("*.ini")
                                                                                                                                 .Where(x => x.Length < 500000)
                                                                                                                                 .Take(100)) {
                            try {
                                writer.Write("SysConfig/" + fileInfo.Name, fileInfo.FullName);
                            } catch (Exception e) {
                                Logging.Warning("Can’t attach SysConfig/" + fileInfo.Name + ": " + e);
                            }
                        }
                    }

                    var raceOut = AcPaths.GetResultJsonFilename();
                    if (File.Exists(raceOut)) {
                        try {
                            writer.Write("Race.json", raceOut);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Race.json:" + e);
                        }
                    }

                    var career = AcPaths.GetKunosCareerProgressFilename(); ;
                    if (File.Exists(career)) {
                        try {
                            writer.Write("Career.ini", career);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Career.ini:" + e);
                        }
                    }

                    foreach (var filename in Directory.GetFiles(logsDirectory, "Main*.log").TakeLast(35)
                                                      .Union(Directory.GetFiles(logsDirectory, "Packed*.log").TakeLast(35))) {
                        var name = Path.GetFileName(filename);
                        try {
                            writer.Write("Logs/" + name, filename);
                        } catch (Exception e) {
                            Logging.Warning("Can’t attach Logs/" + name + ": " + e);
                        }
                    }
                }

                var data = memory.ToArray();
                if (data.Length > 20000000) {
                    File.WriteAllBytes(FilesStorage.Instance.GetTemporaryFilename("Report.zip"), data);
                    throw new Exception("Size limit exceeded");
                }

                InternalUtils.SendAppReport(memory.ToArray(), $@"Name: {GetUserName()};
Operating system: {GetWindowsName()};
App version: {BuildInformation.AppVersion}.", CmApiProvider.UserAgent);
            }
        }

        [Localizable(false)]
        public static void SendData([NotNull] string name, [NotNull] object obj, string notes) {
            using (var memory = new MemoryStream()) {
                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                    try {
                        writer.WriteString(name, obj is string s ? s : JsonConvert.SerializeObject(obj));
                    } catch (Exception e) {
                        Logging.Warning($"Can’t attach {obj}: " + e);
                    }
                }

                var data = memory.ToArray();
                if (data.Length > 20000000) {
                    File.WriteAllBytes(FilesStorage.Instance.GetTemporaryFilename("Data.zip"), data);
                    throw new Exception("Size limit exceeded");
                }

#if DEBUG
                File.WriteAllBytes(FilesStorage.Instance.GetTemporaryFilename("Data.zip"), data);
#endif

                InternalUtils.SendData(memory.ToArray(), $@"Name: {GetUserName()};
Notes: {notes.ToSentenceMember()};
Used language: {CultureInfo.CurrentUICulture.Name};
Operating system: {GetWindowsName()};
App version: {BuildInformation.AppVersion}.", CmApiProvider.UserAgent);
            }
        }
    }
}
