using System;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
        public static class BuildInformation {
        private static string _appVersion;

        [NotNull]
        public static string AppVersion {
            get {
                try {
                    return _appVersion ??
                            (_appVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()?.Location ?? "").FileVersion);
                } catch (Exception e) {
                    return _appVersion = "0";
                }
            }
        }

#if PLATFORM_X86
        public static string Platform { get; private set; } = @"x86";
#elif PLATFORM_X64
        public static string Platform { get; private set; } = @"x64";
#elif PLATFORM_ANYCPU
        public static string Platform { get; private set; } = @"AnyCPU";
#else
        public static string Platform { get; private set; } = @"Unknown";
#endif

#if DEBUG
        public static string Configuration { get; private set; } = @"Debug";
#else
        public static string Configuration { get; private set; } = @"Release";
#endif
   }
}
