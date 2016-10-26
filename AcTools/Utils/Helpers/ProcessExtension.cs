using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Windows;

namespace AcTools.Utils.Helpers {
    public static class ProcessExtension {
        private static string GetQuotedArgument(string argument) {
            // The argument is processed in reverse character order.
            // Any quotes (except the outer quotes) are escaped with backslash.
            // Any sequences of backslashes preceding a quote (including outer quotes) are doubled in length.
            var resultBuilder = new StringBuilder();

            var outerQuotesRequired = HasWhitespace(argument);

            var precedingQuote = false;
            if (outerQuotesRequired) {
                resultBuilder.Append('"');
                precedingQuote = true;
            }

            for (var index = argument.Length - 1; index >= 0; index--) {
                var @char = argument[index];
                resultBuilder.Append(@char);

                if (@char == '"') {
                    precedingQuote = true;
                    resultBuilder.Append('\\');
                } else if (@char == '\\' && precedingQuote) {
                    resultBuilder.Append('\\');
                } else {
                    precedingQuote = false;
                }
            }

            if (outerQuotesRequired) {
                resultBuilder.Append('"');
            }

            return Reverse(resultBuilder.ToString());
        }

        private static bool HasWhitespace(string text) {
            return text.Any(char.IsWhiteSpace);
        }

        private static string Reverse(string text) {
            return new string(text.Reverse().ToArray());
        }

        public static Process Start(string filename, IEnumerable<string> args, bool shell = true) {
            return Process.Start(new ProcessStartInfo {
                FileName = filename,
                Arguments = args.Select(GetQuotedArgument).JoinToString(" "),
                UseShellExecute = shell
            });
        }

        public static bool HasExitedSafe(this Process process) {
            var handle = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryLimitedInformation | Kernel32.ProcessAccessFlags.Synchronize, false, process.Id);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return true;

            try {
                int exitCode;
                if (Kernel32.GetExitCodeProcess(handle, out exitCode) && exitCode != Kernel32.STILL_ACTIVE) return true;
                using (var w = new ProcessWrapper.ProcessWaitHandle(handle)) {
                    return w.WaitOne(0, false);
                }
            } finally {
                Kernel32.CloseHandle(handle);
            }
        }

        private static async Task WaitForExitAsyncDeeperFallback(Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            var processId = process.Id;
            while (true) {
                await Task.Delay(300, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                try {
                    Process.GetProcessById(processId);
                } catch (ArgumentException) {
                    return;
                }
            }
        }

        private static async Task WaitForExitAsyncFallback(Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                while (!process.HasExited) {
                    await Task.Delay(300, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) return;
                }
            } catch (Exception) {
                // throw;
                await WaitForExitAsyncDeeperFallback(process, cancellationToken);
            }
        }

        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                var tcs = new TaskCompletionSource<object>();
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(null);
                if (cancellationToken != default(CancellationToken)) {
                    cancellationToken.Register(() => { tcs.TrySetCanceled(); });
                }

                return tcs.Task;
            } catch (Exception) {
                return WaitForExitAsyncFallback(process, cancellationToken);
            }
        }
    }
}
