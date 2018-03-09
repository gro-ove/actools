using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private FtpClient CreateFtpClient() {
            return new FtpClient($@"ftp://{FtpHost}/", FtpLogin, FtpPassword) {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        private AsyncCommand<CancellationToken?> _ftpVerifyConnectionCommand;

        public AsyncCommand<CancellationToken?> FtpVerifyConnectionCommand => _ftpVerifyConnectionCommand ?? (_ftpVerifyConnectionCommand =
                new AsyncCommand<CancellationToken?>(async c => {
                    try {
                        await CreateFtpClient().DirectoryListSimpleAsync(FtpDirectory);
                        c?.ThrowIfCancellationRequested();
                        Toast.Show("Verified", "FTP parameters verified");
                    } catch (Exception e) when (e.IsCancelled()) {
                        // Do nothing
                    } catch (WebException e) when (e.Response is FtpWebResponse ftp && ftp.StatusCode == FtpStatusCode.NotLoggedIn) {
                        if (c?.IsCancellationRequested == true) return;
                        ModernDialog.ShowMessage("Invalid login or password", "Verification failed", MessageBoxButton.OK);
                    } catch (WebException e) when (e.Response is FtpWebResponse ftp && ftp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) {
                        // Directory doesn’t exist, but will be created later
                        if (c?.IsCancellationRequested == true) return;
                        Toast.Show("Verified", "FTP parameters verified");
                    } catch (Exception e) {
                        if (c?.IsCancellationRequested == true) return;
                        NonfatalError.Notify("Can’t verify FTP parameters", e);
                    }
                }, c => !string.IsNullOrWhiteSpace(FtpHost) && !string.IsNullOrWhiteSpace(FtpLogin) && !string.IsNullOrWhiteSpace(FtpPassword)));

        private AsyncCommand<CancellationToken?> _ftpUploadContentCommand;

        public AsyncCommand<CancellationToken?> FtpUploadContentCommand
            => _ftpUploadContentCommand ?? (_ftpUploadContentCommand = new AsyncCommand<CancellationToken?>(async c => {
                try {
                    var ftpDirectory = FtpDirectory.Replace('\\', '/').TrimStart('/');
                    var ftp = CreateFtpClient();
                    var createdDirectories = new List<string> { ftpDirectory };
                    var temporaryDirectory = FilesStorage.Instance.GetTemporaryDirectory("Server FTP Upload");

                    if (FtpClearBeforeUpload && ModernDialog.ShowMessage($"Are you sure to clear existing files from {FtpHost}/{ftpDirectory}?",
                            "Clear existing files", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                        return;
                    }

                    using (var waiting = new WaitingDialog { FirstAppearDelay = TimeSpan.Zero })
                    using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(waiting.CancellationToken, c ?? CancellationToken.None)) {
                        waiting.Report("Packing…");

                        var cancellation = cancellationSource.Token;
                        var packed = await PackServerData(true, FtpMode, false, cancellation);
                        if (packed == null || cancellation.IsCancellationRequested) return;

                        if (FtpClearBeforeUpload) {
                            try {
                                waiting.Report("Removing existing files…");
                                await ftp.CleanDirectoryAsync(ftpDirectory, cancellation);
                            } catch (WebException e) when ((e.Response as FtpWebResponse)?.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) {
                                waiting.Report("Making sure directory is created…");
                                await EnsureDirectoryExists(ftpDirectory, cancellation);
                                // Not created yet?
                            }
                        } else {
                            waiting.Report("Making sure directory is created…");
                            await EnsureDirectoryExists(ftpDirectory, cancellation);
                        }

                        for (var i = 0; i < packed.Count; i++) {
                            var item = packed[i];
                            waiting.Report(item.Key, i, packed.Count);

                            var filename = item.GetFilename(temporaryDirectory);
                            if (filename == null) continue;

                            var itemPath = string.IsNullOrEmpty(ftpDirectory) ? item.Key : $@"{ftpDirectory}/{item.Key}";
                            await EnsureDirectoryExists(Path.GetDirectoryName(itemPath), cancellation);
                            await ftp.UploadAsync(itemPath, filename, cancellation);
                        }
                    }

                    Toast.Show("Content uploaded", "Content uploaded to FTP server successfully");

                    async Task EnsureDirectoryExists(string directory, CancellationToken ensureDirectoryIsCreatedCancellationToken) {
                        Logging.Debug("Create directory: " + directory);
                        if (string.IsNullOrEmpty(directory) || createdDirectories.Contains(directory)) return;
                        createdDirectories.Add(directory);

                        var parent = Path.GetDirectoryName(directory);
                        if (!string.IsNullOrEmpty(parent)) {
                            await EnsureDirectoryExists(parent, ensureDirectoryIsCreatedCancellationToken).ConfigureAwait(false);
                            ensureDirectoryIsCreatedCancellationToken.ThrowIfCancellationRequested();
                        }

                        try {
                            await ftp.CreateDirectoryAsync(directory, ensureDirectoryIsCreatedCancellationToken).ConfigureAwait(false);
                        } catch (WebException e) when ((e.Response as FtpWebResponse)?.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) {
                            // Already exists? Isn’t FTP lovely…
                        }
                    }
                } catch (Exception e) when (e.IsCancelled()) {
                    // Do nothing
                } catch (WebException e) when (e.Response is FtpWebResponse ftp && ftp.StatusCode == FtpStatusCode.NotLoggedIn) {
                    if (c?.IsCancellationRequested == true) return;
                    NonfatalError.Notify("Can’t upload content to FTP server", "Invalid login or password");
                } catch (Exception e) {
                    if (c?.IsCancellationRequested == true) return;
                    NonfatalError.Notify("Can’t upload content to FTP server", e);
                }
            }, c => !string.IsNullOrWhiteSpace(FtpHost) && !string.IsNullOrWhiteSpace(FtpLogin) && !string.IsNullOrWhiteSpace(FtpPassword)));
    }
}