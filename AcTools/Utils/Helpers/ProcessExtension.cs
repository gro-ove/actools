using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public static string GetQuotedArgument([CanBeNull] string argument) {
            if (string.IsNullOrEmpty(argument)) return "";

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

        [NotNull]
        public static Process Start([NotNull] string filename, [CanBeNull] IEnumerable<string> args, bool useShellExecute = true) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));

            // Manual creation allows to catch Win32Exception
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = filename,
                    Arguments = args?.Select(GetQuotedArgument).JoinToString(" ") ?? "",
                    UseShellExecute = useShellExecute
                }
            };
            process.Start();
            return process;
        }

        [NotNull]
        public static Process Start([Localizable(false), NotNull] string filename, [Localizable(false), CanBeNull] IEnumerable<string> args, ProcessStartInfo startInfo,
                bool enableRaisingEvents = false) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));

            startInfo.FileName = filename;
            startInfo.Arguments = args?.Select(GetQuotedArgument).JoinToString(" ") ?? "";

#if DEBUG
            AcToolsLogging.Write(startInfo.Arguments);
#endif

            // Manual creation allows to catch Win32Exception
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = enableRaisingEvents };
            process.Start();
            return process;
        }

        public static bool HasExitedSafe([NotNull] this Process process) {
            if (process == null) throw new ArgumentNullException(nameof(process));

            int processId;
            try {
                processId = process.Id;
            } catch (InvalidOperationException) {
                // What?
                return true;
            }

            var handle = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryLimitedInformation | Kernel32.ProcessAccessFlags.Synchronize, false, processId);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return true;

            try {
                if (Kernel32.GetExitCodeProcess(handle, out var exitCode) && exitCode != Kernel32.STILL_ACTIVE) return true;
                using (var w = new ProcessWrapper.ProcessWaitHandle(handle)) {
                    return w.WaitOne(0, false);
                }
            } finally {
                Kernel32.CloseHandle(handle);
            }
        }

        private static async Task WaitForExitAsyncDeeperFallback([NotNull] Process process, CancellationToken cancellationToken = default) {
            if (process == null) throw new ArgumentNullException(nameof(process));

            AcToolsLogging.Write("Is there an issue?");

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

        private static async Task WaitForExitAsyncFallback([NotNull] Process process, CancellationToken cancellationToken = default) {
            if (process == null) throw new ArgumentNullException(nameof(process));

            var handle = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryLimitedInformation | Kernel32.ProcessAccessFlags.Synchronize, false, process.Id);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) {
                await WaitForExitAsyncDeeperFallback(process, cancellationToken);
                return;
            }

            try {
                if (Kernel32.GetExitCodeProcess(handle, out var exitCode) && exitCode != Kernel32.STILL_ACTIVE) return;
                using (var w = new ProcessWrapper.ProcessWaitHandle(handle)) {
                    AcToolsLogging.Write("Waiting using ProcessWaitHandle…");

                    while (!w.WaitOne(0, false)) {
                        await Task.Delay(300, cancellationToken);
                        if (cancellationToken.IsCancellationRequested) return;
                    }
                }
            } finally {
                Kernel32.CloseHandle(handle);
            }
        }

        public static Task WaitForExitAsync([NotNull] this Process process, CancellationToken cancellationToken = default) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            try {
                var tcs = new TaskCompletionSource<object>();
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(null);
                if (cancellationToken != default) {
                    cancellationToken.Register(() => { tcs.TrySetCanceled(); });
                }

                return tcs.Task;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return WaitForExitAsyncFallback(process, cancellationToken);
            }
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
                var path = GetProcessPathUsingPsApi(process.Id);
                if (path != null) {
                    AcToolsLogging.Write("PS API: " + path);
                    return path;
                }

                // very slow
                path = GetProcessPathUsingManagement(process.Id);
                if (path != null) {
                    AcToolsLogging.Write("Management: " + path);
                    return path;
                }

                AcToolsLogging.Write("Management failed!");

                // won’t work if processes were compiled for different architectures
                path = process.MainModule?.FileName;
                AcToolsLogging.Write("MainModule.FileName: " + path);
                return path;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        [DllImport(@"psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName,
                [In, MarshalAs(UnmanagedType.U4)] int nSize);

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
            if (gch.Target is List<IntPtr> list) {
                list.Add(handle);
                return true;
            }

            throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
        }

        public static IReadOnlyList<IntPtr> GetWindowsHandles([NotNull] this Process process) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            var handles = new List<IntPtr>();
            foreach (ProcessThread thread in Process.GetProcessById(process.Id).Threads) {
                User32.EnumThreadWindows(thread.Id, (hWnd, lParam) => {
                    handles.Add(hWnd);
                    return true;
                }, IntPtr.Zero);
            }
            return handles;
        }

        public static bool HasWindow([NotNull] this Process process, IntPtr handle) {
            if (process == null) throw new ArgumentNullException(nameof(process));
            var result = false;
            foreach (ProcessThread thread in Process.GetProcessById(process.Id).Threads) {
                User32.EnumThreadWindows(thread.Id, (h, l) => result |= h == handle, IntPtr.Zero);
            }
            return result;
        }
    }
}