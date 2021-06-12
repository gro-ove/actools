using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Steamworks;

namespace AcManager.Tools.Starters {
    public class SteamStarter : StarterBase {
        private static bool _running;
        private static string _acRoot;
        private static string _dllsPath;

        private static async Task RunCallbacks() {
            _running = true;
            while (_running) {
                SteamAPI.RunCallbacks();
                await Task.Delay(500);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeInner() {
            var initialized = SteamAPI.InitSafe();

            for (var i = 0; i < 3 && !initialized; i++) {
                Logging.Write($"Delayed Steam initialization, attempt #{i + 1} {(SteamAPI.IsSteamRunning() ? "" : "(Steam not running)")}");
                Thread.Sleep(150);

                try {
                    SteamAPI.Shutdown();
                } catch (Exception e) {
                    Logging.Write("Steam API shutdown not required: " + e);
                }

                initialized = SteamAPI.Init();
            }

            if (!initialized) {
                Logging.Write("Still not initialized…");

                if (!SteamAPI.RestartAppIfNecessary(new AppId_t(244210u))) {
                    MessageBox.Show("Steam can’t be initialized.", "Steam Inactive", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }

                Environment.Exit(0);
            }

            RunCallbacks().Ignore();
            SteamUtils.SetOverlayNotificationPosition(ENotificationPosition.k_EPositionBottomLeft);

            try {
                if (SteamIdHelper.Instance != null) {
                    SteamIdHelper.Instance.Value = SteamUser.GetSteamID().ToString();
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            AppDomain.CurrentDomain.ProcessExit += (sender, args) => SteamAPI.Shutdown();
        }

        private static bool AreFilesSame(string a, string b) {
            // because of symlinks… not that it’s a common case, but for me, it is
            if (!string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase)) return false;
            if (a == null || b == null) return a == b;

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
            Kernel32.AddDllDirectory(_dllsPath);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }

        public static bool Initialize(string acRoot, bool force) {
            if (IsInitialized) {
                return true;
            }

            _acRoot = acRoot;
            _dllsPath = Path.Combine(_acRoot, "launcher", "support");

            if (!force && !IsAvailable) {
                Logging.Write("Wrong location, SteamStarter won’t work");
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
        private static Dictionary<string, int> GetAchievementsInner() {
            var count = SteamUserStats.GetNumAchievements();
            var tiers = new Dictionary<string, int>();
            for (var i = 0U; i < count; ++i) {
                var name = SteamUserStats.GetAchievementName(i);
                if (SteamUserStats.GetAchievement(name, out var achieved) && achieved && name.StartsWith("SPECIAL_EVENT_")
                        && name[name.Length - 2] == '_' && char.IsDigit(name[name.Length - 1])) {
                    var key = name.Substring(0, name.Length - 2);
                    var value = name[name.Length - 1] - '0';
                    Logging.Debug("NAME: " + name);
                    Logging.Debug("BY: " + SteamUserStats.GetAchievementAchievedPercent(name, out var percent));
                    Logging.Debug("PERCENT: " + percent);
                    if (!tiers.TryGetValue(key, out var prev) || value > prev) {
                        tiers[key] = Math.Max(prev, value);
                    }
                }
            }
            return tiers;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Dictionary<string, double> GetAchievementStatsInner() {
            var count = SteamUserStats.GetNumAchievements();
            var tiers = new Dictionary<string, double>();
            for (var i = 0U; i < count; ++i) {
                var name = SteamUserStats.GetAchievementName(i);
                if (SteamUserStats.GetAchievementAchievedPercent(name, out var percent)) {
                    tiers[name] = percent;
                }
            }
            return tiers;
        }

        [CanBeNull]
        public static Dictionary<string, int> GetAchievements() {
            try {
                return GetAchievementsInner();
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [CanBeNull]
        public static Dictionary<string, double> GetAchievementStats() {
            try {
                return GetAchievementStatsInner();
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
                _running = false;
                SteamFinish();
            } catch {
                // ignored
            }
        }

        public override void Run() {
            RaisePreviewRunEvent(AcsFilename);
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = _acRoot
            });
            if (GameProcess != null && OptionTrackProcess) {
                ChildProcessTracker.AddProcess(GameProcess);
            }
        }

        private static Assembly _assembly;

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
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
            } else {
                MessageDialog.Show("Please, save original launcher as “AssettoCorsa_original.exe” in AC root folder");
            }
        }
    }
}