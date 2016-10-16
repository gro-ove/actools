// #define LOGGING

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace AcStarter {
    internal static class Program {
        private static readonly string DefaultExeFile = "acs_x86.exe";
        private static readonly string StorageFile = "__cm_tricky_starter.txt";
        private static readonly long Id = DateTime.Now.Ticks % 100000;

        private static string Time(string m) {
            var t = DateTime.Now;
            return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}/{4}: {5}", t.Hour, t.Minute, t.Second, t.Millisecond, Id, m);
        }

        private static void Log(string message) {
#if LOGGING
            var logFile = Assembly.GetEntryAssembly().Location + "_cm.log";

            using (var writer = new StreamWriter(logFile, true)) {
                writer.WriteLine(Time(message));
            }
#endif
        }

        private static void FirstStage(string root, string acsName) {
            Log("first stage");

            var storage = Path.Combine(Path.GetTempPath(), StorageFile);
            File.WriteAllText(storage, acsName);

            Log("starting: '" + DefaultExeFile + "'");
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(root, DefaultExeFile) });

            Log("now wait...");
            Thread.Sleep(4000);
        }

        private static void SecondStage(string root) {
            Log("second stage");

            var storage = Path.Combine(Path.GetTempPath(), StorageFile);
            string exeName;

            try {
                exeName = File.ReadAllText(storage);
            } catch (Exception) {
                exeName = DefaultExeFile;
            }

            Log("name: '" + exeName + "'");
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(root, exeName) });

            Log("now wait...");
            Thread.Sleep(4000);
        }

        private static void Main(string[] args) {
            Log("started: ['" + string.Join("', '", args) + "']");

            var root = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (root == null) return;

            if (args.Length == 2 && args[0] == "--first-stage") {
                FirstStage(root, args[1]);
            } else {
                SecondStage(root);
            }

            Log("finished");
        }
    }
}
