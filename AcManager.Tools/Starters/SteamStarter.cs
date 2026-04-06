using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Steamworks;

namespace AcManager.Tools.Starters {
    public class SteamStarter : StarterBase {
        public class SteamInviteArgs {
            public string SteamId;
            public string InviteUrl;

            public SteamInviteArgs(string steamId, string inviteUrl) {
                SteamId = steamId;
                InviteUrl = inviteUrl;
            }
        }

        public static event EventHandler<SteamInviteArgs> SteamInvite;
        
        public static event EventHandler<bool> SteamOverlayShown;

        public static Callback<GameRichPresenceJoinRequested_t> InviteListener { get; private set; }

        public static Callback<GameLobbyJoinRequested_t> LobbyListener { get; private set; }

        public static Callback<LobbyDataUpdate_t> LobbyDataCallback { get; private set; }

        public static Callback<GameOverlayActivated_t> OverlayListener { get; private set; }

        private static bool _running;
        private static string _acRoot;
        private static string _dllsPath;

        public SteamStarter() {
            Logging.Debug("Creating Steam starter…");
            Initialize(AcRootDirectory.Instance.RequireValue, true);
        }

        private static async Task RunCallbacks() {
            _running = true;
            while (_running) {
                SteamAPI.RunCallbacks();
                await Task.Delay(_fpsBoosted ? 20 : 250);
            }
        }

        private static async void BoostCallbacks() {
            try {
                for (var i = 0; i < 100; ++i) {
                    SteamAPI.RunCallbacks();
                    await Task.Delay(20);
                }
            } catch (Exception e) {
                // ignored
            }
        }

        public static bool IsOverlayVisible => _fpsBoosted;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeInner() {
            var initialized = SteamAPI.InitSafe();

            for (var i = 0; i < 3 && !initialized; i++) {
                Logging.Write($"Delayed Steam initialization, attempt #{i + 1} {(SteamAPI.IsSteamRunning() ? "" : "(Steam not running)")}");
                Thread.Sleep(50);

                try {
                    SteamAPI.Shutdown();
                } catch (Exception e) {
                    Logging.Write("Steam API shutdown not required: " + e);
                }

                initialized = SteamAPI.Init();
            }

            if (!initialized) {
                if (!IsFullyIntegrated) {
                    MessageBox.Show("Failed to initialize Steam. Make sure Steam is running, or change the starter in Drive settings to something else.",
                            "Steam in not responding", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                } else {
                    Logging.Write("Still not initialized, asking Steam to restart AC…");
                    if (!SteamAPI.RestartAppIfNecessary(new AppId_t(244210u))) {
                        MessageBox.Show("Failed to initialize Steam. Make sure Steam is running, or change the starter in Drive settings to something else.",
                                "Steam in not responding", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    }
                }
                Logging.Write("Steam failed to initialize, existing.");
                Environment.Exit(0);
            }

            Logging.Debug($"Steam API is ready: {SteamUser.GetSteamID()}");
            RunCallbacks().Ignore();
            InviteListener = Callback<GameRichPresenceJoinRequested_t>.Create(t =>
                    SteamInvite?.Invoke(null, new SteamInviteArgs(t.m_steamIDFriend.ToString(), t.m_rgchConnect)));
            LobbyListener = Callback<GameLobbyJoinRequested_t>.Create(t => GetLobbyInviteUrlTyped(t.m_steamIDLobby).ContinueWith(v => {
                if (v.IsCompleted && !string.IsNullOrEmpty(v.Result)) {
                    SteamInvite?.Invoke(null, new SteamInviteArgs(t.m_steamIDFriend.ToString(), v.Result));
                }
            }));
            OverlayListener = Callback<GameOverlayActivated_t>.Create(t => {
                Logging.Debug($"Overlay: {t.m_bActive}");
                BoostCallbacks();
                if ((t.m_bActive != 0) != _fpsBoosted) {
                    _fpsBoosted = !_fpsBoosted;
                    SteamOverlayShown?.Invoke(null, _fpsBoosted);
                }
            });
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

        private static bool _fpsBoosted; 
        private static readonly EventHandler OverlayFpsBoost = (sender, args) => { };

        private static string TryAccessLobbyData(CSteamID lobbyId) {
            var directlyAccessed = SteamMatchmaking.GetLobbyData(lobbyId, "cm.invite.race");
            if (string.IsNullOrEmpty(directlyAccessed)) return null;
            if (SteamMatchmaking.GetLobbyOwner(lobbyId) == SteamUser.GetSteamID()) return "own";
            return "acmanager://race/" + directlyAccessed;
        }

        private static Task<string> GetLobbyInviteUrlTyped(CSteamID lobbyId) {
            var directlyAccessed = TryAccessLobbyData(lobbyId);
            if (directlyAccessed != null) {
                return Task.FromResult(directlyAccessed);
            }

            var tcs = new TaskCompletionSource<string>();
            LobbyDataCallback?.Unregister();
            ActionExtension.InvokeInMainThreadAsync(() => BoostCallbacks());
            LobbyDataCallback = Callback<LobbyDataUpdate_t>.Create(t => {
                if (tcs != null) {
                    var inviteData = t.m_bSuccess != 0 ? TryAccessLobbyData(lobbyId) : null;
                    Logging.Debug($"Lobby invite argument: {lobbyId}, {inviteData}");
                    SteamMatchmaking.LeaveLobby(lobbyId);
                    tcs.SetResult(inviteData);
                    tcs = null;
                    LobbyDataCallback?.Unregister();
                }
            });
            if (!SteamMatchmaking.RequestLobbyData(lobbyId)) {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Logging.Warning("Failed to get lobby data");
                LobbyDataCallback?.Unregister();
                return Task.FromResult<string>(null);
            }

            Task.Delay(5000).ContinueWith(r => {
                if (tcs != null) {
                    SteamMatchmaking.LeaveLobby(lobbyId);
                    tcs.SetResult(string.Empty);
                    tcs = null;
                    LobbyDataCallback?.Unregister();
                }
            });
            return tcs.Task;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task<string> GetLobbyInviteUrlAsyncInner(string lobbyId) {
            Logging.Debug("Trying to get invite URL from lobby (string): " + lobbyId);
            var lobbyIdTyped = new CSteamID(lobbyId.As(0ul));
            return GetLobbyInviteUrlTyped(lobbyIdTyped);
        }

        [ItemCanBeNull]
        public static async Task<string> GetLobbyInviteUrlAsync(string lobbyId) {
            try {
                await InitializeAsync();
                return await GetLobbyInviteUrlAsyncInner(lobbyId).ConfigureAwait(false);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }
        
        private static readonly List<object> AwaitedCalls = new List<object>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task<T> ToTask<T>(SteamAPICall_t apiCall) {
            var completionSource = new TaskCompletionSource<T>();
            var obj = new CallResult<T>[1];
            obj[0] = CallResult<T>.Create((callResult, failure) => {
                Logging.Debug($"callResult={callResult}, failure={failure}");
                if (failure) {
                    completionSource.TrySetException(new InformativeException("Steam API exception",
                            "Please make sure Steam API DLLs in your Assetto Corsa folder are original. Running integrity check on Steam might help."));
                } else {
                    completionSource.TrySetResult(callResult);
                }
                if (AwaitedCalls.Count == 0) {
                    Logging.Warning("Should not be happening");
                }
                AwaitedCalls.Remove(obj[0]);
            });
            obj[0].Set(apiCall);
            AwaitedCalls.Add(obj[0]);
            return completionSource.Task;
        }

        private static ulong _previousLobbyId;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task InviteFriendAsyncInner(string inviteLinkBase) {
            ActionExtension.InvokeInMainThreadAsync(() => BoostCallbacks());
            var curLobbyId = new CSteamID(_previousLobbyId);
            if (!curLobbyId.IsValid() || SteamMatchmaking.GetLobbyOwner(curLobbyId) != SteamUser.GetSteamID()) {
                Logging.Debug("Creating new lobby");

                /*var result = CallResult<LobbyCreated_t>.Create((callResult, failure) => {
                    Logging.Debug($"callResult={callResult}, failure={failure}");
                });
                var callId = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, 2);
                result.Set(callId);
                for (var i = 0; i < 1000; ++i) {
                    Logging.Debug("Result: " + result);
                    await Task.Delay(50);
                }*/
                
                var lobby = await ToTask<LobbyCreated_t>(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, 2));
                Logging.Debug("Lobby created: " + lobby.m_eResult);
                if (lobby.m_eResult != EResult.k_EResultOK) {
                    throw new InformativeException($"Failed to create Steam lobby for an invite ({lobby})",
                            "Please make sure Steam API DLLs in your Assetto Corsa folder are original. Running integrity check on Steam might help.");
                }
                curLobbyId = new CSteamID(lobby.m_ulSteamIDLobby);
                _previousLobbyId = curLobbyId.m_SteamID; 
                SteamMatchmaking.SetLobbyData(curLobbyId, "cm.invite.race", inviteLinkBase);
            }
            SteamFriends.ActivateGameOverlayInviteDialog(curLobbyId);
        }

        public static async Task InviteFriendAsync(string inviteLinkBase) {
            await InitializeAsync();
            await InviteFriendAsyncInner(inviteLinkBase).ConfigureAwait(false);
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

        private static bool? isFullyIntegrated;

        public static bool IsFullyIntegrated {
            get {
                if (isFullyIntegrated == null) {
                    isFullyIntegrated = AreFilesSame(MainExecutingFile.Location, LauncherFilename);
                }
                return isFullyIntegrated.Value;
            }
        }

        public static bool IsInitialized { get; private set; }

        private static void InitializeLibraries() {
            Kernel32.AddDllDirectory(_dllsPath); // not really needed, seems like steamworks DLL loads DLL from its location
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }

        public static Task<bool> InitializeAsync() {
            if (IsInitialized) {
                return Task.FromResult(true);
            }

            return Task.Run(() => Initialize(AcRootDirectory.Instance.RequireValue, true));
        }

        public static bool Initialize(string acRoot, bool force) {
            if (IsInitialized) {
                return true;
            }

            _acRoot = acRoot;
            _dllsPath = Path.Combine(_acRoot, "launcher", "support");

            if (!force && !IsFullyIntegrated) {
                Logging.Write("Wrong location, SteamStarter won’t work");
                return false;
            }

            try {
                InitializeLibraries();
            } catch (Exception e) {
                Logging.Error(e);
            }

            var steamAppId = Path.Combine(MainExecutingFile.Directory, "steam_appid.txt");
            var steamTagNeeded = !File.Exists(steamAppId);
            try {
                if (steamTagNeeded) {
                    File.WriteAllText(steamAppId, @"244210");
                }
            } catch (Exception e) {
                Logging.Warning($"Failed to create Steam ID file: {e}");
            }

            try {
                InitializeInner();
                IsInitialized = true;
                return true;
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            } finally {
                if (steamTagNeeded) {
                    FileUtils.TryToDelete(steamAppId);
                }
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushRichPresenceInner([CanBeNull] string state, [CanBeNull] string details) {
            // Doesn’t work without localized strings :(
        }

        public static void PushRichPresence([CanBeNull] string state, [CanBeNull] string details) {
            if (!IsInitialized) return;
            try {
                PushRichPresenceInner(state, details);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        [ItemCanBeNull]
        public static async Task<Dictionary<string, int>> GetAchievementsAsync() {
            try {
                await InitializeAsync().ConfigureAwait(false);
                return GetAchievementsInner();
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<Dictionary<string, double>> GetAchievementStatsAsync() {
            try {
                await InitializeAsync().ConfigureAwait(false);
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