using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Steamworks;
using Application = System.Windows.Application;

namespace AcManager.Tools.Starters {
    public class SteamStarter : StarterBase {
        private static string _acRoot;
        private static string _dllsPath;

        private static async Task RunCallbacks() {
            while (true) {
                SteamAPI.RunCallbacks();
                await Task.Delay(500);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeInner() {
            var initialized = SteamAPI.InitSafe();

            for (var i = 0; i < 3 && !initialized; i++) {
                Logging.Debug($"Delayed Steam initialization, attempt #{i + 1} {(SteamAPI.IsSteamRunning() ? "" : "(Steam not running)")}");
                Thread.Sleep(150);

                try {
                    SteamAPI.Shutdown();
                } catch (Exception e) {
                    Logging.Debug("Steam API shutdown not required: " + e);
                }

                initialized = SteamAPI.Init();
            }

            if (!initialized) {
                Logging.Debug("Still not initialized…");

                if (!SteamAPI.RestartAppIfNecessary(new AppId_t(244210u))) {
                    MessageBox.Show("Steam can’t be initialized.", "Steam Inactive", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }

                Environment.Exit(0);
            }

            RunCallbacks().Forget();
            SteamUtils.SetOverlayNotificationPosition(ENotificationPosition.k_EPositionBottomLeft);

            AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                SteamAPI.Shutdown();
            };
        }

        private static bool AreFilesSame(string a, string b) {
            // because of symlinks… not that it’s a common case, but for me, it is
            if (!string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase)) return false;

            var ai = new FileInfo(a);
            var bi = new FileInfo(b);
            return ai.LastWriteTime == bi.LastWriteTime && ai.CreationTime == bi.CreationTime &&
                    ai.Length == bi.Length;
        }

        private static bool? _isAvailable;

        public static bool IsAvailable {
            get {
                if (_isAvailable == null) {
                    _isAvailable = AreFilesSame(MainExecutingFile.Location, LauncherFilename);
                }

                return _isAvailable.Value;
            }
        }

        public static bool IsInitialized { get; private set; }

        private static void InitializeLibraries() {
            // Environment.SetEnvironmentVariable("PATH", $"{Environment.GetEnvironmentVariable("PATH")};{_dllsPath}");
            // LoadLibrary(Path.Combine(_dllsPath, "CSteamworks.dll"));
            // LoadLibrary(Path.Combine(_dllsPath, "steam_api.dll"));
            Kernel32.AddDllDirectory(_dllsPath);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }

        public static bool Initialize(string acRoot) {
            if (IsInitialized) {
                return true;
            }

            _acRoot = acRoot;
            _dllsPath = Path.Combine(_acRoot, "launcher", "support");

            if (!IsAvailable) {
                Logging.Debug("Wrong location, SteamStarter won’t work");
                return false;
            }

            try {
                InitializeLibraries();
            } catch (Exception e) {
                Logging.Error(e);
            }

            try {
                InitializeInner();
                IsInitialized = true;
                return true;
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetAchievementsInner() {
            var reader = Path.Combine(_acRoot, "SteamStatisticsReader.exe");
            var output = new StringBuilder();
            using (var process = new Process {
                StartInfo = {
                    FileName = reader,
                    WorkingDirectory = _acRoot,
                    Arguments = "-c",
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            }) {
                process.Start();
                process.OutputDataReceived += (sender, args) => {
                    if (args.Data != null) output.Append(args.Data);
                };
                process.BeginOutputReadLine();
                process.WaitForExit(10000);
                if (!process.HasExited) {
                    process.Kill();
                }
            }

            var result = Regex.Replace(output.ToString(), @"\r+|\s+|\n$", "");
            return string.IsNullOrEmpty(result) ? "{}" : result;
        }

        [CanBeNull]
        public static string GetAchievements() {
            try {
                return GetAchievementsInner();
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SteamFinish() {
            SteamAPI.Shutdown();
        }

        private static void OnExit(object sender, EventArgs e) {
            try {
                SteamFinish();
            } catch {
                // ignored
            }
        }

        public override void Run() {
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = _acRoot
            });
        }

        private static Assembly _assembly;

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            Logging.Debug(new AssemblyName(args.Name).Name);
            return new AssemblyName(args.Name).Name == "Steamworks.NET"
                    ? (_assembly ?? (_assembly = Assembly.LoadFrom(Path.Combine(_dllsPath, "Steamworks.NET.dll")))) : null;
        }

        private static string LauncherFilename => Path.Combine(_acRoot, "AssettoCorsa.exe");

        private static string LauncherOriginalFilename => Path.Combine(_acRoot, "AssettoCorsa_original.exe");

        public static void StartOriginalLauncher() {
            var filename = LauncherOriginalFilename;
            if (File.Exists(filename)) {
                Process.Start(new ProcessStartInfo {
                    FileName = filename,
                    WorkingDirectory = _acRoot
                });
            }
        }
    }
}