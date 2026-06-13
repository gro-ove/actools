using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AcTools.Windows {
    [Localizable(false)]
    public static class WineHelper {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private static int _runningOnWine;

        public static bool IsOnWine {
            get {
                if (_runningOnWine == 0) {
                    try {
                        var ntdll = GetModuleHandle("ntdll.dll");
                        _runningOnWine = ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero ? 2 : 1;
                    } catch (Exception) {
                        _runningOnWine = 1;
                    }
                }
                return _runningOnWine == 2;
            }
        }
    }
}