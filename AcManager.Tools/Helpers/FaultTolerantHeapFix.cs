using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class FaultTolerantHeapFix {
        private static IEnumerable<string> GetRelatedValues(RegistryKey stateKey) {
            return stateKey.GetValueNames().Where(name => string.Equals(Path.GetFileName(name), @"acs.exe", StringComparison.OrdinalIgnoreCase));
        }

        private static bool Check() {
            try {
                using (var localMachineKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32))
                using (var stateKey = localMachineKey.OpenSubKey(@"Software\Microsoft\FTH\State")) {
                    return stateKey != null && GetRelatedValues(stateKey).Any();
                }
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
        }

        private static async Task FixAsync() {
            var patch = new RegistryPatch();
            using (var localMachineKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32)) {
                using (var fthKey = localMachineKey.OpenSubKey(@"Software\Microsoft\FTH"))
                using (var stateKey = fthKey?.OpenSubKey(@"State")) {
                    if (stateKey == null) {
                        throw new Exception(@"FTH key is missing");
                    }

                    if (!(fthKey.GetValue("ExclusionList") is string[] exclusionList)) {
                        throw new Exception(@"Exclusion list is missing");
                    }

                    patch[@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\FTH"][@"ExclusionList"] = exclusionList.Append(@"acs.exe").Append(@"acs_pro.exe").Distinct().ToArray();
                    foreach (var name in GetRelatedValues(stateKey).ToList()) {
                        patch[@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\FTH\State"][name] = null;
                    }
                }

                using (var layersKey = localMachineKey.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers")) {
                    if (layersKey != null) {
                        foreach (var name in GetRelatedValues(layersKey).ToList()) {
                            patch[@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"][name] = null;
                        }
                    }
                }
            }
            await patch.ApplyAsync("Potential performance issue detected",
                    "[url=\"https://docs.microsoft.com/en-us/windows/win32/win7appqual/fault-tolerant-heap\"]Fault Tolerant Heap[/url] might be considerably slowing down loading process of Assetto Corsa, but to disable it, a few values in Windows Registry have to be changed, and that would require administrator privilegies. Would you like for Content Manager to disable it automatically, or just prepare a .reg-file for you to inspect and import manually?[br][br]You might need to restart your Windows for changes to apply.", "FTHFix.reg");
        }

        public static async Task CheckAsync() {
            if (Check()) {
                try {
                    await FixAsync();
                } catch (Exception e) {
                    Logging.Warning(e);
                    NonfatalError.Notify("Failed to fix the issue with FTH", e);
                }
            }
        }
    }
}