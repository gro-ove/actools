using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class FaultTolerantHeapFix {
        private static IEnumerable<string> GetRelatedValues(RegistryKey stateKey) {
            return stateKey.GetValueNames().Where(name => string.Equals(Path.GetFileName(name), @"acs.exe", StringComparison.OrdinalIgnoreCase));
        }

        public static bool Check() {
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

                    patch[@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\FTH"][@"ExclusionList"] =
                            exclusionList.Append(@"acs.exe").Append(@"acs_pro.exe").Distinct().ToArray();

                    var relatedValues = GetRelatedValues(stateKey).ToList();
                    foreach (var name in relatedValues) {
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
                    "[url=\"https://docs.microsoft.com/en-us/windows/win32/win7appqual/fault-tolerant-heap\"]Fault Tolerant Heap[/url] might be considerably slowing down loading process of Assetto Corsa, but to disable it, a few values in Windows Registry have to be changed, and that would require administrator privilegies. Would you like for Content Manager to disable it automatically, or just prepare a .reg-file for you to inspect and import manually?[br][br]You might need to restart your Windows for changes to apply.",
                    "FixFTH.reg");
            for (var i = 0; i < 120; ++i) {
                await Task.Delay(TimeSpan.FromSeconds(1d));
                if (!Check()) {
                    await ResetService();
                    break;
                }
            }
        }

        private static async Task ResetService() {
            var secondResponse = ActionExtension.InvokeInMainThread(() => MessageDialog.Show(
                    "Would you like to reset FTH system to make sure changes are applied? Otherwise, you would need to restart Windows.[br][br]It’ll need to run this command:[br][mono]Rundll32.exe fthsvc.dll,FthSysprepSpecialize[/mono][br][br]Or, Content Manager can simply prepare a .bat-file for you to inspect and run manually. [url=\"https://docs.microsoft.com/en-us/windows/win32/win7appqual/fault-tolerant-heap\"]Learn more[/url].",
                    "One more thing",
                    new MessageDialogButton(MessageBoxButton.YesNo, MessageBoxResult.Yes) {
                        [MessageBoxResult.Yes] = "Reset automatically",
                        [MessageBoxResult.No] = "Prepare a .bat-file only"
                    }));
            if (secondResponse == MessageBoxResult.Cancel) return;

            var filename = FilesStorage.Instance.GetTemporaryFilename("RunElevated", "FixFTH.bat");
            File.WriteAllText(filename, @"@echo off
:: More info: https://docs.microsoft.com/en-us/windows/win32/win7appqual/fault-tolerant-heap
echo Running rundll32.exe fthsvc.dll,FthSysprepSpecialize...
cd %windir%\system32
%windir%\system32\rundll32.exe fthsvc.dll,FthSysprepSpecialize
echo Done
pause");

            if (secondResponse == MessageBoxResult.Yes) {
                var procRunDll32 = ProcessExtension.Start("explorer.exe", new[] { filename }, new ProcessStartInfo { Verb = "runas" });
                await procRunDll32.WaitForExitAsync().ConfigureAwait(false);
                Logging.Debug("Done: " + procRunDll32.ExitCode);
            } else if (secondResponse == MessageBoxResult.No) {
                WindowsHelper.ViewFile(filename);
            }
        }

        public static async Task CheckAndFixAsync() {
            if (Check()) {
                ValuesStorage.Set(".fth.shown", true);
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