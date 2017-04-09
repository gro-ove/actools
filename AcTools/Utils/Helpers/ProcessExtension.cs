using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ProcessExtension {
        private static string GetQuotedArgument([NotNull] string argument) {
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

        private static bool HasWhitespace([NotNull] string text) {
            return text.Any(char.IsWhiteSpace);
        }

        private static string Reverse([NotNull] string text) {
            return new string(text.Reverse().ToArray());
        }

        public static Process Start([NotNull] string filename, [CanBeNull] IEnumerable<string> args, bool shell = true) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            return Process.Start(new ProcessStartInfo {
                FileName = filename,
                Arguments = args?.Select(GetQuotedArgument).JoinToString(" ") ?? "",
                UseShellExecute = shell
            });
        }

        public static bool HasExitedSafe([NotNull] this Process process) {
            if (process == null) throw new ArgumentNullException(nameof(process));
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

        private static async Task WaitForExitAsyncDeeperFallback([NotNull] Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            if (process == null) throw new ArgumentNullException(nameof(process));
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

        private static async Task WaitForExitAsyncFallback([NotNull] Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            try {
                while (!process.HasExited) {
                    await Task.Delay(300, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) return;
                }
#if DEBUG
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                await WaitForExitAsyncDeeperFallback(process, cancellationToken);
            }
#else
            } catch (Exception) {
                await WaitForExitAsyncDeeperFallback(process, cancellationToken);
            }
#endif
        }

        public static Task WaitForExitAsync([NotNull] this Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            try {
                var tcs = new TaskCompletionSource<object>();
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(null);
                if (cancellationToken != default(CancellationToken)) {
                    cancellationToken.Register(() => { tcs.TrySetCanceled(); });
                }

                return tcs.Task;
#if DEBUG
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return WaitForExitAsyncFallback(process, cancellationToken);
            }
#else
            } catch (Exception) {
                return WaitForExitAsyncFallback(process, cancellationToken);
            }
#endif
        }

        /// <summary>
        /// Might be very slow (up to ≈700ms) if GetProcessPathUsingPsApi won’t work properly.
        /// Returns null when all three ways failed.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <returns>Path to process’s executable file.</returns>
        [CanBeNull]
        public static string GetFilenameSafe([NotNull] this Process process) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            try {
                return GetProcessPathUsingPsApi(process.Id) ?? GetProcessPathUsingManagement(process.Id) ??
                        process.MainModule.FileName; // won’t work if processes were compiled for different architectures
            } catch (Exception) {
                return null;
            }
        }

        [DllImport(@"psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        private static string GetProcessPathUsingPsApi(int pid) {
            var processHandle = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryInformation, false, pid);
            if (processHandle == IntPtr.Zero) return null;

            const int lengthSb = 4000;

            try {
                var sb = new StringBuilder(lengthSb);
                return GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, lengthSb) > 0 ? sb.ToString() : null;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            } finally {
                Kernel32.CloseHandle(processHandle);
            }
        }

        [CanBeNull]
        private static string GetProcessPathUsingManagement(int processId) {
            try {
                using (var s = new ManagementObjectSearcher($"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}"))
                using (var c = s.Get()) {
                    return c.Cast<ManagementObject>().Select(x => x[@"ExecutablePath"]).FirstOrDefault()?.ToString();
                }
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }

            return null;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer) {
            var gch = GCHandle.FromIntPtr(pointer);
            var list = gch.Target as List<IntPtr>;
            if (list == null) throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            list.Add(handle);
            return true;
        }

        public static IReadOnlyList<IntPtr> GetWindowsHandles([NotNull] this Process process) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            var handles = new List<IntPtr>();
            foreach (ProcessThread thread in Process.GetProcessById(process.Id).Threads) {
                User32.EnumThreadWindows(thread.Id, (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);
            }
            return handles;
        }
    }
}
