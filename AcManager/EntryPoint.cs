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
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.Forms.MessageBox;

namespace AcManager {
    [Localizable(false)]
    public static class EntryPoint {
        private static bool _initialized;

        public static string ApplicationDataDirectory { get; private set; }

        public static string GetLogName(string id) {
            if (AppArguments.GetBool(AppFlag.SingleLogFile)) {
                return Path.Combine(ApplicationDataDirectory, "Logs", $"{id}.log");
            }

            var now = DateTime.Now;
            return Path.Combine(ApplicationDataDirectory, "Logs",
                    $"{id}_{now.Year % 100:D2}{now.Month:D2}{now.Day:D2}_{now.Hour:D2}{now.Minute:D2}{now.Second:D2}.log");
        }

        private static bool Rename(string oldName, string newName) {
            var old = Path.Combine(ApplicationDataDirectory, oldName);
            if (!Directory.Exists(old)) return false;

            var renamed = Path.Combine(ApplicationDataDirectory, newName);
            if (!Directory.Exists(renamed)) {
                try {
                    Directory.Move(old, renamed);
                } catch (Exception) {
                    // ignored
                }

                return true;
            }

            return false;
        }

        private static void RenameContentToData() {
            if (Rename("Content", "Data")) {
                Rename("Content (User)", "Data (User)");
                Rename(@"Data\Data", @"Data\Miscellaneous");
                Rename(@"Data (User)\Data", @"Data (User)\Miscellaneous");
            }
        }

        [STAThread]
        private static void Main(string[] a) {
            if (Type.GetType("System.Web.HttpResponse, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")?
                    .GetMethod("AddOnSendingHeaders") == null
                    && MessageBox.Show("It appears that .NET 4.5.2 is not installed. Would you like to download it first?", "Error",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes) {
                Process.Start("https://www.microsoft.com/net/download/dotnet-framework-runtime/net452");
                return;
            }

            try {
                MainReal(a);
            } catch (Exception e) {
                UnhandledExceptionHandler(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MainReal(string[] a) {
            if (!Debugger.IsAttached) {
                SetUnhandledExceptionHandler();
            }

            AppArguments.Initialize(a);
            var data = AppArguments.Get(AppFlag.StorageLocation);
            if (!string.IsNullOrWhiteSpace(data)) {
                ApplicationDataDirectory = Path.GetFullPath(data);
            } else {
                var exe = Assembly.GetEntryAssembly().Location;
                ApplicationDataDirectory = Path.GetFileName(exe).IndexOf("local", StringComparison.OrdinalIgnoreCase) != -1
                        ? Path.Combine(Path.GetDirectoryName(exe) ?? Path.GetFullPath("."), "Data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcTools Content Manager");
            }

            if (AppArguments.GetBool(AppFlag.VisualStyles, true)) {
                Application.EnableVisualStyles();
            }

            AppArguments.AddFromFile(Path.Combine(ApplicationDataDirectory, "Arguments.txt"));
            RenameContentToData();

            var logFilename = AppArguments.GetBool(AppFlag.LogPacked) ? GetLogName("Packed Log") : null;
            AppArguments.Set(AppFlag.DirectAssembliesLoading, ref PackedHelper.OptionDirectLoading);

            var packedHelper = new PackedHelper("AcTools_ContentManager", "References", logFilename);
            packedHelper.PrepareUnmanaged("LevelDB");
            AppDomain.CurrentDomain.AssemblyResolve += packedHelper.Handler;

            MainInner(a);
        }

        private static uint _secondInstanceMessage;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MainInner(string[] args) {
            if (AppUpdater.OnStartup(args)) return;

            if (args.Length == 2 && args[0] == "--run") {
                Process.Start(args[1]);
                return;
            }

            var appGuid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), true).GetValue(0)).Value;
            var mutexId = $@"Global\{{{appGuid}}}";

            if (args.Contains(WindowsHelper.RestartArg)) {
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
                    _initialized = true;
                    RunApp();
                } else {
                    PassArgsToRunningInstance(args);
                }
            }
        }

        private static void RunApp() {
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

        [Localizable(false)]
        private static string PackArgs(IEnumerable<string> args) {
            return args.Select(x => "\n" + x.Replace(@"\", @"\\").Replace("\n", @"\n")).JoinToString();
        }

        [Localizable(false)]
        private static IEnumerable<string> UnpackArgs(string packed) {
            return packed.Split('\n').Select(x => x.Replace(@"\n", "\n").Replace(@"\\", @"\")).Skip(1);
        }

        public static void PassSomeData(IEnumerable<string> data) {
            try {
                File.WriteAllText(CommunicationFilename, PackArgs(data));
            } catch (Exception) {
                // ignored
            }
        }

        private static IEnumerable<string> ReceiveSomeData() {
            if (!File.Exists(CommunicationFilename)) return new string[] { };

            try {
                var text = File.ReadAllText(CommunicationFilename);
                File.Delete(CommunicationFilename);
                return UnpackArgs(text);
            } catch (Exception e) {
                Logging.Warning("Cannot receive data: " + e);
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
            var app = System.Windows.Application.Current;
            if (app != null) {
                app.Dispatcher.Invoke(() => {
                    UnhandledExceptionHandler(e);
                });
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
                        MessageBox.Show(text, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                app.Dispatcher.Invoke(() => {
                    UnhandledExceptionHandler(e);
                });
            } else {
                UnhandledExceptionHandler(e);
            }
        }

        public static void HandleSecondInstanceMessages(Window window, Action<IEnumerable<string>> handler) {
            HwndSource hwnd = null;
            HwndSourceHook hook = (IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (message == _secondInstanceMessage) {
                    try {
                        handler(ReceiveSomeData());
                        (window as DpiAwareWindow)?.BringToFront();
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
