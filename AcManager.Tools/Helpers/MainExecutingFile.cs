using System.IO;

namespace AcManager.Tools.Helpers {
    public static class MainExecutingFile {
        private static string _location;

        public static string Location => _location ?? (_location = System.Reflection.Assembly.GetEntryAssembly().Location);

        private static string _directory;

        public static string Directory => _directory ?? (_directory = Path.GetDirectoryName(Location));
    }
}
