using System;

namespace AcManager.Tools.ContentInstallation {
    public static class NewFilesReporter {
        public static EventHandler<string> NewFileCreated;
        
        public static void RegisterNewFile(string filename) {
            NewFileCreated.Invoke(null, filename);
        }
    }
}