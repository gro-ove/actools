using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class WindowsHelper {
        public const string RestartArg = "--restart";

        public static void RestartCurrentApplication() {
            try {
                ProcessExtension.Start(MainExecutingFile.Location, Environment.GetCommandLineArgs().Skip(1).ApartFrom(RestartArg).Prepend(RestartArg));
                if (Application.Current != null) {
                    Application.Current.Shutdown();
                } else {
                    Environment.Exit(0);
                }
            } catch (Exception e) {
                Logging.Warning("[WindowsHelper] RestartCurrentApplication(): " + e);
            }
        }

        public static void ViewDirectory(string directory) {
            ProcessExtension.Start(@"explorer", new [] { directory });
        }

        public static void ViewFile(string filename) {
            Process.Start("explorer", "/select," + filename);
        }

        // TODO
        public static void ViewInBrowser([Localizable(false)] string url) {
            if (url == null) return;
            Process.Start(url);
        }
    }
}
