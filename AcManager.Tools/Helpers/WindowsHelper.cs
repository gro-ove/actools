using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers {
    public static class WindowsHelper {
        public const string RestartArg = "--restart";

        public static void RestartCurrentApplication() {
            ProcessExtension.Start(MainExecutingFile.Location, Environment.GetCommandLineArgs().Skip(1).Prepend(RestartArg));
            Application.Current.Shutdown();
        }

        public static void ViewDirectory(string directory) {
            ProcessExtension.Start(@"explorer", new [] { directory });
        }

        public static void ViewFile(string filename) {
            Process.Start("explorer", "/select," + filename);
        }

        // TODO
        public static void ViewInBrowser(string url) {
            if (url == null) return;
            Process.Start(url);
        }
    }
}
