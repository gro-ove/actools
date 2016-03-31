using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class BuildInformation {
        private static string _appVersion;

        [NotNull]
        public static string AppVersion => _appVersion ??
                                    (_appVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileVersion);
    }
}
