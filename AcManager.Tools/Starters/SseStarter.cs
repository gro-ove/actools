using System;
using System.Diagnostics;
using System.IO;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Starters {
    public class SseStarter : BaseStarter {
        public static string OptionStartName = null;

        public const string AddonId = "SSE";

        private const string AcSteamId = "244210";
        private const string ConfigName = "_.ini";

        private string _filename;

        protected override string AcsName => OptionStartName ?? base.AcsName;

        public override void Run() {
            var acRoot = AcRootDirectory.Instance.RequireValue;
            var addon = PluginsManager.Instance.GetById(AddonId);
            if (addon?.IsReady != true) throw new Exception("Addon isn’t ready");

            _filename = addon.GetFilename(ConfigName);

            Logging.Debug(AcsName);

            var defaultConfig = addon.GetFilename("config.ini");
            if (File.Exists(defaultConfig)) {
                new IniFile(defaultConfig) {
                    ["Launcher"] = {
                        ["Target"] = Path.Combine(acRoot, AcsName),
                        ["StartIn"] = acRoot,
                        ["SteamClientPath"] = addon.GetFilename("sse86.dll"),
                        ["SteamClientPath64"] = addon.GetFilename("sse64.dll")
                    },
                }.Save(_filename);
            } else {
                new IniFile {
                    ["Launcher"] = {
                        ["Target"] = Path.Combine(acRoot, AcsName),
                        ["StartIn"] = acRoot,
                        ["InjectDll"] = false,
                        ["SteamClientPath"] = addon.GetFilename("sse86.dll"),
                        ["SteamClientPath64"] = addon.GetFilename("sse64.dll")
                    },
                    ["Achievements"] = {
                        ["UnlockAll"] = true
                    },
                    ["Debug"] = {
                        ["EnableLog"] = true,
                        ["Minidump"] = true,
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