using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class PatchHelper {
        public static bool OptionPatchSupport = false;

        public static readonly string MainFileName = "dwrite.dll";

        public static event EventHandler Reloaded;

        public static string GetMainFilename() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, MainFileName);
        }

        public static string GetRootDirectory() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, "extension");
        }

        public static string GetManifestFilename() {
            return Path.Combine(GetRootDirectory(), "config", "data_manifest.ini");
        }

        public static string GetInstalledLog() {
            return Path.Combine(GetRootDirectory(), "installed.log");
        }

        private static IniFile _manifest;
        private static Lazier<Tuple<string, string>> _installed;

        static PatchHelper() {
            _installed = Lazier.Create(() => {
                var installedLogFilename = GetInstalledLog();
                if (!File.Exists(installedLogFilename)) return Tuple.Create<string, string>(null, null);

                string version = null, build = null;
                foreach (var line in File.ReadAllLines(installedLogFilename).Select(x => x.Trim()).Where(x => !x.StartsWith(@"#"))) {
                    var keyValue = line.Split(new[] { ':' }, 2, StringSplitOptions.None);
                    if (keyValue.Length != 2) continue;
                    switch (keyValue[0].Trim()) {
                        case "version":
                            version = keyValue[1].Trim();
                            break;
                        case "build":
                            build = keyValue[1].Trim();
                            break;
                    }
                }

                return Tuple.Create(version, build);
            });
        }

        public static IniFile GetManifest() {
            return _manifest ?? (_manifest = new IniFile(GetManifestFilename()));
        }

        [CanBeNull]
        public static string GetInstalledVersion() {
            return GetManifest()["VERSION"].GetNonEmpty("SHADERS_PATCH");
            // return _installed.Value?.Item1;
        }

        [CanBeNull]
        public static string GetInstalledBuild() {
            return GetManifest()["VERSION"].GetNonEmpty("SHADERS_PATCH_BUILD");
            // return _installed.Value?.Item2;
        }

        public static void Reload() {
            _manifest = null;
            _installed.Reset();
            ActionExtension.InvokeInMainThreadAsync(() => {
                Reloaded?.Invoke(null, EventArgs.Empty);
            });
        }
    }
}