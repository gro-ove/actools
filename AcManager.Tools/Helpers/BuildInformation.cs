using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class BuildInformation {
        private static string _appVersion;

        [NotNull]
        public static string AppVersion => _appVersion ??
                                    (_appVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location ?? "").FileVersion);

#if DEBUG
#if PLATFORM_X86
        public static string Platform { get; private set; } = @"x86; Debug";
#elif PLATFORM_X64
        public static string Platform { get; private set; } = @"x64; Debug";
#elif PLATFORM_ANYCPU
        public static string Platform { get; private set; } = @"AnyCPU; Debug";
#else
        public static string Platform { get; private set; } = @"Unknown; Debug";
#endif
#else
#if PLATFORM_X86
        public static string Platform { get; private set; } = @"x86";
#elif PLATFORM_X64
        public static string Platform { get; private set; } = @"x64";
#elif PLATFORM_ANYCPU
        public static string Platform { get; private set; } = @"AnyCPU";
#else
        public static string Platform { get; private set; } = @"Unknown";
#endif
#endif
    }
}
