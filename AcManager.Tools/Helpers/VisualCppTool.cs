using System;
using System.Collections.Generic;
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
            Kernel32.AddDllDirectory(directory);
        }

        private static bool _shown;
        private static List<string> _previousMessages = new List<string>();

        public static bool OnException([CanBeNull] Exception e, string fallbackMessage) {
            if (IsVisualCppRelatedException(e)) {
                var msg = e?.ToString();
                if (!_previousMessages.Contains(msg)) {
                    _previousMessages.Add(msg);
                    Logging.Error(msg);
                }

                if (_shown) {
                    ShowFallbackMessage(e, fallbackMessage);
                } else {
                    _shown = true;
                    if (IsVisualCppInstalled()) {
                        NonfatalError.Notify("Looks like app can’t load native library, even though it’s installed", "Maybe it’s damaged?", e);
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
            var checksumsFile = Path.Combine(directory, "Checksums.txt");
            if (!File.Exists(checksumsFile)) return false;

            try {
                return File.ReadAllLines(checksumsFile).All(s => {
                    s = s.Trim();
                    if (s.StartsWith(@"#")) return true;

                    var x = s.Split(new[] { @" *" }, StringSplitOptions.RemoveEmptyEntries);
                    if (x.Length != 2) return true;

                    var filename = Path.Combine(directory, x[1]);
                    if (!File.Exists(filename)) return false;
                    var bytes = File.ReadAllBytes(filename);
                    using (var sha1 = SHA1.Create()) {
                        return string.Equals(sha1.ComputeHash(bytes).ToHexString(), x[0], StringComparison.OrdinalIgnoreCase);
                    }
                });
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