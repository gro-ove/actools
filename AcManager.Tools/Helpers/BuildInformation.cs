using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class BuildInformation {
        private static string _appVersion;

        [NotNull]
        public static string AppVersion => _appVersion ??
                                    (_appVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileVersion);
    }
}
