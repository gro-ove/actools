using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AcManager {
    [Localizable(false)]
    public static partial class EntryPoint {
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

            var profileDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcTools Content Manager",
                    "Temporary", "Profile");
            Directory.CreateDirectory(profileDirectory);
            // ProfileOptimization.SetProfileRoot(profileDirectory);
            // ProfileOptimization.StartProfile("Startup.Profile");

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
                var exe = Assembly.GetEntryAssembly()?.Location;
                ApplicationDataDirectory = Path.GetFileName(exe)?.IndexOf("local", StringComparison.OrdinalIgnoreCase) != -1
                        ? Path.Combine(Path.GetDirectoryName(exe) ?? Path.GetFullPath("."), "Data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcTools Content Manager");
            }

            if (AppArguments.GetBool(AppFlag.VisualStyles, true)) {
                Application.EnableVisualStyles();
            }

            AppArguments.AddFromFile(Path.Combine(ApplicationDataDirectory, "Arguments.txt"));
            RenameContentToData();

#if COSTURA
            CosturaUtility.Initialize();
#else
            var logFilename = AppArguments.GetBool(AppFlag.LogPacked) ? GetLogName("Packed Log") : null;
            AppArguments.Set(AppFlag.DirectAssembliesLoading, ref PackedHelper.OptionDirectLoading);

            var packedHelper = new PackedHelper("AcTools_ContentManager", "References", logFilename);
            packedHelper.PrepareUnmanaged("LevelDB");
            AppDomain.CurrentDomain.AssemblyResolve += packedHelper.Handler;
#endif

            MainInner(a);
        }
    }
}