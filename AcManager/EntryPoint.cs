using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager {
    public class EntryPoint {
        private static string _applicationDataDirectory;

        public static string ApplicationDataDirectory => _applicationDataDirectory ?? (_applicationDataDirectory =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcTools Content Manager"));

        [STAThread]
        private static void Main(string[] a) {
            if (!Debugger.IsAttached) {
                SetUnhandledExceptionHandler();
            }
            
            AppArguments.Initialize(a.Skip(1));
            AppArguments.AddFromFile(Path.Combine(ApplicationDataDirectory, "Arguments.txt"));
            LocalesHelper.Initialize();

            AppDomain.CurrentDomain.AssemblyResolve += new PackedHelper("AcTools_ContentManager", "AcManager.References",
                    Assembly.GetEntryAssembly().Location.Contains(@"_log_packed")).Handler;
            MainInner(a);
        }

        public static uint SecondInstanceMessage { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void MainInner(string[] args) {
            if (AppArguments.GetBool(AppFlag.UseCustomLocales, true)) {
                var customLocale = Path.Combine(ApplicationDataDirectory, @"Locales", CultureInfo.CurrentUICulture.Name);
                if (Directory.Exists(customLocale)) {
                    CustomResourceManager.SetCustomSource(customLocale);
                }
            }

            if (AppUpdater.OnStartup(args)) return;

            var appGuid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), true).GetValue(0)).Value;
            var mutexId = $@"Global\{{{appGuid}}}";

            if (args.Contains(WindowsHelper.RestartArg)) {
                for (var i = 0; i < 999; i++) {
                    Thread.Sleep(200);

                    using (var mutex = new Mutex(false, mutexId)) {
                        if (mutex.WaitOne(0, false)) {
                            break;
                        }
                    }
                }
                
                ProcessExtension.Start(MainExecutingFile.Location, args.Where(x => x != WindowsHelper.RestartArg));
                return;
            }
            
            using (var mutex = new Mutex(false, mutexId)) {
                SecondInstanceMessage = User32.RegisterWindowMessage(mutexId);

                if (mutex.WaitOne(0, false)) {
                    var app = new App();
                    app.Run();
                } else {
                    PassArgsToRunningInstance(args);
                }
            }
        }

        private static string CommunicationFilename => Path.Combine(Path.GetTempPath(), "__CM_SnappySandybrownTerrier_811447478013131345");

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

        public static IEnumerable<string> ReceiveSomeData() {
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
            User32.PostMessage(User32.HWND_BROADCAST, SecondInstanceMessage, 0, 0);
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public static void SetUnhandledExceptionHandler() {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += UnhandledExceptionHandler;
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
        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            var e = args.ExceptionObject as Exception;

            var text = string.Format(Resources.Main_UnhandledException, (e?.ToString() ?? @"?"));
            try {
                // ErrorMessage.ShowWithoutLogging("Unhandled exception", "Please, send MainLog.txt to developer.", e);
                MessageBox.Show(text, Controls.Resources.Common_Oops, MessageBoxButtons.OK, MessageBoxIcon.Error);
            } catch (Exception) {
                // ignored
            }

            if (!LogError(text)) {
                try {
                    var logFilename = $"{AppDomain.CurrentDomain.BaseDirectory}/content_manager_crash_{DateTime.Now.Ticks}.txt";
                    File.WriteAllText(logFilename, text);
                } catch (Exception) {
                    // ignored
                }
            }

            Environment.Exit(1);
        }
    }
}
