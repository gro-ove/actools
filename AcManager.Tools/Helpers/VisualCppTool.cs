using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class VisualCppTool {
        [CanBeNull]
        private static string _directory;

        public static void Initialize([CanBeNull] string directory) {
            _directory = directory;

            if (directory != null && Directory.Exists(directory)) {
                var s = Stopwatch.StartNew();
                var backup = Environment.CurrentDirectory;
                try {
                    Environment.CurrentDirectory = directory;
                    Kernel32.SetDllDirectory(directory);
                    Environment.SetEnvironmentVariable(@"PATH", Environment.GetEnvironmentVariable("PATH") + Path.PathSeparator + directory);
                    foreach (var file in Directory.GetFiles(directory, "*.dll")) {
                        Kernel32.LoadLibrary(file);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                } finally {
                    Environment.CurrentDirectory = backup;
                }

                Logging.Write($"Libraries load time: {s.Elapsed.TotalMilliseconds:F1} ms");
                Logging.Write($"Current directory: {Environment.CurrentDirectory}");
            }
        }

        private static bool _shown;
        private static readonly List<string> PreviousMessages = new List<string>();

        public static bool OnException([CanBeNull] Exception e, string fallbackMessage) {
            if (IsVisualCppRelatedException(e)) {
                var msg = e?.ToString();
                if (!PreviousMessages.Contains(msg)) {
                    PreviousMessages.Add(msg);
                    Logging.Error(msg);
                }

                if (_shown) {
                    ShowFallbackMessage(e, fallbackMessage);
                } else {
                    _shown = true;
                    if (IsVisualCppInstalled()) {
                        NonfatalError.Notify("Looks like app can’t load native library, even though it’s installed",
                                "Maybe it’s damaged? Or, possibly, some system libraries are missing.", e);
                    } else {
                        ShowMessage(e);
                    }
                }
                return true;
            }

            ShowFallbackMessage(e, fallbackMessage);
            return false;
        }

        private static void ShowFallbackMessage(Exception e, string fallbackMessage) {
            if (fallbackMessage != null) {
                NonfatalError.Notify(fallbackMessage, e);
            }
        }

        private static bool IsVisualCppRelatedException(Exception e) {
            var s = e.ToString();
            return s.Contains(@"SlimDX, Version") || s.Contains(@"slimdx.dll") || s.Contains(@"System.BadImageFormatException");
        }

        private static bool IsVisualCppInstalled() {
            var directory = _directory ?? MainExecutingFile.Directory;
            var checksumsFile = Path.Combine(directory, "VisualCppManifest.txt");
            if (!File.Exists(checksumsFile)) return false;

            try {
                string version = null;
                return File.ReadAllLines(checksumsFile).All(s => {
                    s = s.Trim();
                    if (s.StartsWith(@"#")) {
                        var keyValue = s.TrimStart('#', ' ').Split(new[] { ':' }, 2);
                        if (keyValue.Length == 2) {
                            var key = keyValue[0].Trim().ToLowerInvariant();
                            var value = keyValue[1].Trim();
                            Logging.Debug($"{key}={version}");
                            switch (key) {
                                case "version":
                                    version = value;
                                    break;
                            }
                        }
                        return true;
                    }

                    var x = s.Split(new[] { @" *" }, StringSplitOptions.RemoveEmptyEntries);
                    if (x.Length != 2) return true;

                    var filename = Path.Combine(directory, x[1]);
                    if (!File.Exists(filename)) return false;
                    var bytes = File.ReadAllBytes(filename);
                    using (var sha1 = SHA1.Create()) {
                        var actual = sha1.ComputeHash(bytes).ToHexString();
                        if (string.Equals(actual, x[0], StringComparison.OrdinalIgnoreCase)) return true;
                        Logging.Warning($"Checksums don’t match for {x[1]}: {x[0]}≠{actual}");
                        return false;
                    }
                }) && !version.IsVersionOlderThan(@"2");
            } catch (Exception e) {
                Logging.Error(e);
                return false;
            }
        }

        private static void ShowMessage(Exception e) {
            NonfatalError.Notify("Looks like app can’t load native library",
                    "Visual C++ Redistributable might be missing or damaged. Would you like to install the package prepared specially for CM?", e, new[] {
                        new NonfatalErrorSolution("Download and install", null, DownloadAndInstall, "DownloadIconData"),
                    });
        }

        private static async Task DownloadAndInstall(CancellationToken token) {
            var directory = _directory ?? MainExecutingFile.Directory;

            try {
                var data = await CmApiProvider.GetStaticDataAsync("visual_cpp", TimeSpan.FromDays(1), cancellation: token);
                if (data == null) {
                    ManualInstallation(null, directory);
                } else {
                    await Task.Run(() => {
                        using (var archive = ZipFile.OpenRead(data.Item1)) {
                            foreach (var file in archive.Entries) {
                                var completeFileName = Path.Combine(directory, file.FullName);
                                if (file.Name == "" || File.Exists(completeFileName)) continue;
                                FileUtils.EnsureFileDirectoryExists(completeFileName);
                                file.ExtractToFile(completeFileName, true);
                            }
                        }
                    });

                    FileUtils.TryToDelete(data.Item1);
                    if (ModernDialog.ShowMessage("The package is installed. Now, app needs to be restarted. Restart it now?", "The package is installed",
                            MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        WindowsHelper.RestartCurrentApplication();
                    }
                }
            } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                ManualInstallation(e, directory);
            }
        }

        private static void ManualInstallation([CanBeNull] Exception e, string directory) {
            var downloadUrl = @"https://drive.google.com/uc?id=1vPW58x0AsD3XzSSq8MzN9FEuZt6vGTLq";
            NonfatalError.Notify("Can’t download the package",
                    $"You can try to [url={BbCodeBlock.EncodeAttribute(downloadUrl)}]download[/url] and install it manually. Don’t forget to restart CM after it was installed.",
                    e, new[] {
                        new NonfatalErrorSolution("Open destination folder", null, t => {
                            WindowsHelper.ViewDirectory(directory);
                            return Task.Delay(0);
                        }, "FolderIconData"),
                    });
        }
    }
}