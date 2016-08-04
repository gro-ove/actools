using System;
using System.Diagnostics;
using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.DataFile;

namespace AcManager.Tools.Starters {
    public class SseStarter : BaseStarter {
        public const string AddonId = "SSE";

        private const string AcSteamId = "244210";
        private const string ConfigName = "_.ini";

        private string _filename;

        public override void Run() {
            var acRoot = AcRootDirectory.Instance.RequireValue;
            var addon = PluginsManager.Instance.GetById(AddonId);
            if (addon?.IsReady != true) throw new Exception("Addon isn’t ready");

            _filename = addon.GetFilename(ConfigName);

            new IniFile {
                ["Launcher"] = {
                    ["Target"] = Path.Combine(acRoot, AcsName),
                    ["StartIn"] = acRoot,
                    ["SteamClientPath"] = addon.GetFilename("sse86.dll"),
                    ["SteamClientPath64"] = addon.GetFilename("sse64.dll")
                },
                ["Achievements"] = {
                    ["UnlockAll"] = true
                },
                ["SSEOverlay"] = {
                    ["DisableOverlay"] = true
                },
                ["SmartSteamEmu"] = {
                    ["AppId"] = AcSteamId,
                    ["Offline"] = true,
                }
            }.SaveAs(_filename);

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