using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class RunProcessAsyncHelper {
        public static async Task RunAsync(string filename, [Localizable(false)] IEnumerable<string> args, string workingDirectory,
                bool checkErrorCode, IProgress<double?> progress, CancellationToken cancellationToken, Action<string> errorCallback, Func<string, string> errorMessageCleanup = null) {
            var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? string.Empty,
            });
            try {
                ChildProcessTracker.AddProcess(process);
                cancellationToken.ThrowIfCancellationRequested();

                var errorData = new StringBuilder();
                process.ErrorDataReceived += (sender, eventArgs) => {
                    if (eventArgs.Data == null) return;
                    if (errorData.Length > 0) errorData.Append('\n');
                    errorData.Append(eventArgs.Data);
                };
                process.BeginErrorReadLine();

                process.OutputDataReceived += (sender, eventArgs) => {
                    if (eventArgs.Data == null || progress == null) return;
                    var v = eventArgs.Data.As<double?>();
                    if (v.HasValue) progress.Report(v.Value / 100d);
                };
                process.BeginOutputReadLine();

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (errorData.Length > 0) {
                    var data = errorData.ToString().Trim();
                    if (data.Length > 0) {
                        Logging.Warning("Error message from LOD gen: " + data);
                        errorCallback?.Invoke(data);
                    }
                }
                if (checkErrorCode && process.ExitCode != 0) {
                    var errorMessage = errorData.ToString().Trim();
                    if (string.IsNullOrEmpty(errorMessage)) {
                        errorMessage = $@"Failed to run LOD gen: {process.ExitCode}";
                    } else if (errorMessageCleanup != null) {
                        errorMessage = errorMessageCleanup(errorMessage); 
                    } else {
                        var separator = errorMessage.LastIndexOf(@": ", StringComparison.Ordinal);
                        if (separator != -1) {
                            errorMessage = errorMessage.Substring(separator + 2);
                        }
                    }
                    throw new Exception(errorMessage);
                }
            } finally {
                try {
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }
                } catch (Exception killEx) {
                    Logging.Debug($"Failed to kill LOD gen process: {killEx.Message}");
                }
                process.Dispose();
            }
        }
    }
}