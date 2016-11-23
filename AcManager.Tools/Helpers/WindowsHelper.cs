using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class WindowsHelper {
        public const string RestartArg = "--restart";

        [Localizable(false), ContractAnnotation("=> halt")]
        public static void RestartCurrentApplication() {
            try {
                ProcessExtension.Start(MainExecutingFile.Location,
                        Environment.GetCommandLineArgs()
                                   .Skip(1)
                                   .ApartFrom(RestartArg)
                                   .Where(x => !x.StartsWith("acmanager:", StringComparison.OrdinalIgnoreCase))
                                   .Prepend(RestartArg));
                if (Application.Current != null) {
                    Application.Current.Shutdown();
                } else {
                    Environment.Exit(0);
                }
            } catch (Exception e) {
                Logging.Warning("RestartCurrentApplication(): " + e);
            }
        }
        
        [Localizable(false)]
        public static void ViewDirectory([NotNull] string directory) {
            ProcessExtension.Start(@"explorer", new [] { directory });
        }

        [Localizable(false)]
        public static void ViewFile([NotNull] string filename) {
            Process.Start("explorer", "/select," + filename);
        }

        [Localizable(false)]
        public static void ViewInBrowser([CanBeNull] string url) {
            if (url == null) return;
            Process.Start(url);
        }

        [Localizable(false)]
        public static void ViewInBrowser([CanBeNull] Uri url) {
            ViewInBrowser(url?.AbsoluteUri);
        }

        [Localizable(false)]
        public static void OpenFile([NotNull] string filename) {
            Process.Start(filename);
        }
    }
}
