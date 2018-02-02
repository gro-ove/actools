using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.CookerHood;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Starters {
    public class SseStarter : StarterBase {
        public static string OptionStartName = null;
        public static bool OptionLogging = false;

        public const string AddonId = "SSE";

        private const string AcSteamId = "244210";
        private const string ConfigName = "_.ini";

        private string _filename;

        protected override string AcsName => OptionStartName ?? (Use32BitVersion ? "acs_x86_chood.exe" : "acs_chood.exe");

        private static bool _initialized;
        private static Assembly _assembly;

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            var name = new AssemblyName(args.Name);
            if (name.Name == "AcTools.CookerHood") {
                if (_assembly == null) {
                    var addon = PluginsManager.Instance.GetById(AddonId);
                    if (addon == null) return null;

                    try {
                        _assembly = Assembly.LoadFrom(addon.GetFilename("cookerHood.dll"));
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }

                return _assembly;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string PrepareExecutableInternal(string acRoot) {
            Logging.Debug(AcsName);

            var target = new FileInfo(Path.Combine(acRoot, AcsName));
            var original = new FileInfo(Path.Combine(acRoot, base.AcsName));

            if (!original.Exists) {
                throw new FileNotFoundException(original.FullName);
            }

            if (!target.Exists || target.LastWriteTime < original.LastWriteTime) {
                Logging.Write("Cooker hood is in action…");
                switch (CookerHood.Process(original.FullName, s => Logging.Debug("(CookerHood) " + s))) {
                    case true:
                        Logging.Write("Cooker hood: done");
                        break;

                    case false:
                        Logging.Warning("Cooker hood: broken");
                        throw new Exception("Can’t prepare executable");

                    case null:
                        // patching not needed?
                        Logging.Warning("Cooker hood: cleaning not needed?");
                        File.Copy(original.FullName, target.FullName);
                        break;
                }
            }

            return target.FullName;
        }

        private string PrepareExecutable(string acRoot) {
            if (!_initialized) {
                _initialized = true;
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }

            return PrepareExecutableInternal(acRoot);
        }

        public override void Run() {
            var acRoot = AcRootDirectory.Instance.RequireValue;
            var addon = PluginsManager.Instance.GetById(AddonId);
            if (addon?.IsReady != true) throw new Exception("Addon isn’t ready");

            _filename = addon.GetFilename(ConfigName);
            var target = PrepareExecutable(acRoot);

            var defaultConfig = addon.GetFilename("config.ini");
            if (File.Exists(defaultConfig)) {
                new IniFile(defaultConfig) {
                    ["Launcher"] = {
                        ["Target"] = target,
                        ["StartIn"] = acRoot,
                        ["SteamClientPath"] = addon.GetFilename("sse86.dll"),
                        ["SteamClientPath64"] = addon.GetFilename("sse64.dll")
                    },
                }.Save(_filename);
            } else {
                new IniFile {
                    ["Launcher"] = {
                        ["Target"] = target,
                        ["StartIn"] = acRoot,
                        ["InjectDll"] = false,
                        ["SteamClientPath"] = addon.GetFilename("sse86.dll"),
                        ["SteamClientPath64"] = addon.GetFilename("sse64.dll")
                    },
                    ["Achievements"] = {
                        ["UnlockAll"] = true
                    },
                    ["Debug"] = {
                        ["EnableLog"] = OptionLogging,
                        ["Minidump"] = OptionLogging,
                    },
                    ["SSEOverlay"] = {
                        ["DisableOverlay"] = true,
                        ["OnlineMode"] = false,
                    },
                    ["SmartSteamEmu"] = {
                        ["AppId"] = AcSteamId,
                        ["SteamIdGeneration"] = "Manual",
                        ["ManualSteamId"] = SteamIdHelper.Instance.Value,
                        ["Offline"] = true,
                        ["EnableOverlay"] = false,
                        ["EnableHTTP"] = false,
                        ["DisableGC"] = true,
                        ["DisableLeaderboard"] = true,
                        ["DisableFriendList"] = true,
                        ["VR"] = true,
                    }
                }.Save(_filename);
            }

            RaisePreviewRunEvent(AcsFilename);
            LauncherProcess = Process.Start(new ProcessStartInfo {
                FileName = addon.GetFilename("sse.exe"),
                WorkingDirectory = addon.Directory,
                Arguments = ConfigName
            });
        }

        public override void CleanUp() {
            base.CleanUp();
            if (File.Exists(_filename)) {
                File.Delete(_filename);
            }
        }
    }
}