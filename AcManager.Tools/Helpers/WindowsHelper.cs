using System;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers {
    public static class WindowsHelper {
        public static void RestartCurrentApplication() {
            ProcessExtension.Start(MainExecutingFile.Location, new[] { "--restart" }.Union(Environment.GetCommandLineArgs().Skip(1)));
            Application.Current.Shutdown();
        }
    }
}
