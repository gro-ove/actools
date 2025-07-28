using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MessageBox = System.Windows.Forms.MessageBox;

namespace AcManager {
    public static partial class EntryPoint {
        private static uint _secondInstanceMessage;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MainInner(string[] args) {
            if (AppUpdater.OnStartup(args)) return;

            if (string.Equals(MainExecutingFile.Name, "SteamStatisticsReader.exe", StringComparison.OrdinalIgnoreCase)
                    && SteamAchievementsReader.Handle(args)) {
                return;
            }

            if (args.Length == 2 && args[0] == "--run") {
                Process.Start(args[1]);
                return;
            }

            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null) {
                return;
            }

            var appGuid = ((GuidAttribute)assembly.GetCustomAttributes(typeof(GuidAttribute), true).GetValue(0)).Value;
            var mutexId = $@"Global\{{{appGuid}}}";

            if (Array.IndexOf(args, WindowsHelper.RestartArg) != -1) {
                for (var i = 0; i < 999; i++) {
                    Thread.Sleep(200);

                    using (var mutex = new Mutex(false, mutexId)) {
                        if (mutex.WaitOne(0, false)) break;
                    }
                }

                ProcessExtension.Start(MainExecutingFile.Location, args.Where(x => x != WindowsHelper.RestartArg));
                return;
            }

            using (var mutex = new Mutex(false, mutexId)) {
                _secondInstanceMessage = User32.RegisterWindowMessage(mutexId);
                if (mutex.WaitOne(0, false)) {
                    if (AppUpdater.OnUniqueStartup(args)) return;

                    _initialized = true;
                    App.CreateAndRun(false);
                    /*if (args.Length == 0) {
                        TryToRunAppSafely();
                    } else {
                        App.CreateAndRun(false);
                    }*/
                } else {
                    PassArgsToRunningInstance(args);
                }
            }
        }

        private static void TryToRunAppSafely() {
            var tryingToRunFlag = Path.Combine(ApplicationDataDirectory, "Trying to run.flag");
            FileUtils.EnsureFileDirectoryExists(tryingToRunFlag);

            var failedLastTime = File.Exists(tryingToRunFlag);
            if (failedLastTime) {
                FileUtils.TryToDelete(tryingToRunFlag);
                App.CreateAndRun(true);
                return;
            }

            var createdOnce = false;
            DpiAwareWindow.NewWindowCreated += OnWindowCreated;
            App.CreateAndRun(false);

            void OnWindowCreated(object sender, EventArgs args) {
                if (App.IsSoftwareRenderingModeEnabled()) {
                    DpiAwareWindow.NewWindowCreated -= OnWindowCreated;
                    return;
                }

                var window = (DpiAwareWindow)sender;
                if (TryToCreate()) {
                    if (window.Content == null) {
                        window.Content = new Border();
                    }

                    window.ContentRendered += OnWindowRendered;
                }
            }

            async void OnWindowRendered(object sender, EventArgs args) {
                var window = (DpiAwareWindow)sender;
                window.ContentRendered -= OnWindowRendered;

                await Task.Yield();
                if (!_crashed) {
                    FileUtils.TryToDelete(tryingToRunFlag);
                }
            }

            bool TryToCreate() {
                if (createdOnce || File.Exists(tryingToRunFlag)) return false;
                createdOnce = true;
                File.WriteAllBytes(tryingToRunFlag, new byte[0]);
                DpiAwareWindow.NewWindowCreated -= OnWindowCreated;
                return true;
            }
        }

        private static string CommunicationFilename => Path.Combine(Path.GetTempPath(), "__cm_cuymbo6r");

        [NotNull, Localizable(false)]
        private static string PackArgs([CanBeNull] IEnumerable<string> args) {
            return args?.Select(x => "\n" + x.Replace(@"\", @"\\").Replace("\n", @"\n")).JoinToString() ?? "";
        }

        [NotNull, Localizable(false)]
        private static IEnumerable<string> UnpackArgs([CanBeNull] string packed) {
            return packed?.Split('\n').Select(x => x.Replace(@"\n", "\n").Replace(@"\\", @"\")).Skip(1) ?? new string[0];
        }

        public static void PassSomeData(IEnumerable<string> data) {
            try {
                File.WriteAllText(CommunicationFilename, $"{Environment.CurrentDirectory}\n{PackArgs(data)}");
            } catch (Exception) {
                // ignored
            }
        }

        [NotNull]
        private static IEnumerable<string> ReceiveSomeData([CanBeNull] out string currentDirectory) {
            if (!File.Exists(CommunicationFilename)) {
                currentDirectory = null;
                return new string[] { };
            }

            try {
                var text = File.ReadAllText(CommunicationFilename).Split(new[] { '\n' }, 2);
                FileUtils.TryToDelete(CommunicationFilename);
                currentDirectory = text.FirstOrDefault();
                return UnpackArgs(text.ArrayElementAtOrDefault(1));
            } catch (Exception e) {
                Logging.Warning(e);
                currentDirectory = null;
                return new string[] { };
            }
        }

        private static void PassArgsToRunningInstance(IEnumerable<string> args) {
            PassSomeData(args);
            User32.PostMessage(User32.HWND_BROADCAST, _secondInstanceMessage, 0, 0);
        }

        private static bool _crashed;

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        private static void SetUnhandledExceptionHandler() {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

#if DEBUG
            TaskScheduler.UnobservedTaskException += UnobservedTaskException;
#endif
        }

        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static void UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args) {
            _crashed = true;
            var e = args.Exception as Exception;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null) {
                dispatcher.Invoke(() => { UnhandledExceptionHandler(e); });
            } else {
                UnhandledExceptionHandler(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool LoggingIsAvailable() {
            return Logging.IsInitialized();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool LogError(string text) {
            try {
                if (!LoggingIsAvailable()) return false;
                Logging.Error(text);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnhandledExceptionFancyHandler(Exception e) {
            if (System.Windows.Application.Current?.Windows.OfType<Window>().Any(x => x.IsLoaded && x.IsVisible) != true) {
                throw new Exception();
            }

            FatalErrorHandler.OnFatalError(e);
        }

        [MethodImpl(MethodImplOptions.NoInlining), HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static void UnhandledExceptionHandler(Exception e) {
            if (e is InvalidOperationException && e.Message.Contains("Visibility") && e.Message.Contains("WindowInteropHelper.EnsureHandle")) {
                Logging.Error(e.ToString());
                return;
            }

            var text = e?.ToString() ?? @"?";
            if (e is COMException && e.StackTrace.Contains("WaitOneNative")) {
                text = "Unable to ensure app runs in a single instance, might be related to antivirus checks.";
            }

            if (!_initialized) {
                try {
                    MessageBox.Show(text, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } catch (Exception) {
                    // ignored
                }
                Environment.Exit(1);
            }

            if (!LogError(text)) {
                try {
                    var logFilename = $"{AppDomain.CurrentDomain.BaseDirectory}/content_manager_crash_{DateTime.Now.Ticks}.txt";
                    File.WriteAllText(logFilename, text);
                } catch (Exception) {
                    // ignored
                }
            }

            if (e is AccessViolationException) {
                MessageBox.Show(text, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else {
                try {
                    UnhandledExceptionFancyHandler(e);
                    return;
                } catch (Exception ex) {
                    LogError(ex.Message);

                    try {
                        if (text.Contains("SlimDX")) {
                            var ret = MessageBox.Show(
                                    "Seems like SlimDX has failed to initialize. Usually, it could be due to Visual Studio 2015 32-bit component "
                                            + "missing or being damaged. Do you want to open the webpage for you to download the package from and install manually? Just "
                                            + "make sure to download and install 32-bit/x86 version.\n\n" + text, "Fatal Error",
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                            if (ret == DialogResult.Yes) {
                                WindowsHelper.ViewInBrowser("https://www.microsoft.com/en-us/download/details.aspx?id=48145");
                            }
                        } else {
                            MessageBox.Show(text, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    } catch (Exception) {
                        // ignored
                    }
                }
            }

            Environment.Exit(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining), HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            _crashed = true;
            var e = args.ExceptionObject as Exception;
            var app = System.Windows.Application.Current;
            if (app != null) {
                app.Dispatcher?.Invoke(() => { UnhandledExceptionHandler(e); });
            } else {
                UnhandledExceptionHandler(e);
            }
        }

        public static void HandleSecondInstanceMessages(Window window, Func<IEnumerable<string>, bool> handler) {
            HwndSource hwnd = null;
            HwndSourceHook hook = (IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (message == _secondInstanceMessage) {
                    try {
                        var data = ReceiveSomeData(out var currentDirectory);
                        if (!string.IsNullOrWhiteSpace(currentDirectory)) {
                            Environment.CurrentDirectory = currentDirectory;
                        }
                        if (handler(data)) {
                            (window as DpiAwareWindow)?.BringToFront();
                        }
                    } catch (Exception e) {
                        Logging.Warning("Can’t handle message: " + e);
                    }
                }

                return IntPtr.Zero;
            };

            RoutedEventHandler[] handlers = { null, null };

            // loaded
            handlers[0] = (sender, args) => {
                window.Loaded -= handlers[0];

                try {
                    hwnd = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                    if (hwnd == null) {
                        Logging.Warning("Can’t add one-instance hook: HwndSource is null");
                        return;
                    }

                    hwnd.AddHook(hook);
                    window.Unloaded += handlers[1];
                } catch (Exception e) {
                    Logging.Warning("Can’t add one-instance hook: " + e);
                    hook = null;
                }
            };

            // unloaded
            handlers[1] = (sender, args) => {
                window.Unloaded -= handlers[1];

                try {
                    hwnd.RemoveHook(hook);
                } catch (Exception e) {
                    Logging.Warning("Can’t remove one-instance hook: " + e);
                    hook = null;
                }
            };

            if (!window.IsLoaded) {
                window.Loaded += handlers[0];
            } else {
                handlers[0](null, null);
            }
        }
    }
}