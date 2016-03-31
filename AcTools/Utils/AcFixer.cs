using System.IO;

namespace AcTools.Utils {
    public static class AcFixer {
        public static void CheckAndFixDefaultLauncher(string acRoot) {
            var acLauncher = FileUtils.GetAcLauncherFilename(acRoot);
            var acLauncherBackup = acLauncher + "~at_bak";

            if (File.Exists(acLauncherBackup)) {
                FileUtils.Recycle(acLauncher);
                File.Move(acLauncherBackup, acLauncher);
            }
        }
    }
}
