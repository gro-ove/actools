using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;

namespace AcManager {
    public class EntryPoint {
        public static uint SecondInstanceMessage { get; private set; }

        [STAThread]
        public static void Main(string[] args) {
            if (AppUpdater.OnStartup(args)) {
                return;
            }

            var appGuid = ((GuidAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value;
            var mutexId = $@"Global\{{{appGuid}}}";

            if (args.Contains("--restart")) {
                for (var i = 0; i < 999; i++) {
                    Thread.Sleep(200);

                    using (var mutex = new Mutex(false, mutexId)) {
                        if (mutex.WaitOne(0, false)) {
                            break;
                        }
                    }
                }

                ProcessExtension.Start(MainExecutingFile.Location, args.Skip(1));
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

        private static string PackArgs(IEnumerable<string> args) {
            return args.Select(x => "\n" + x.Replace(@"\", @"\\").Replace("\n", @"\n")).JoinToString();
        }

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
    }
}
