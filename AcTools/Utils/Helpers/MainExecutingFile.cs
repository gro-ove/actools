using System;
using System.IO;
using System.Reflection;

namespace AcTools.Utils.Helpers {
    public static class MainExecutingFile {
        private static string _location;
        private static string _name;
        private static string _directory;

        public static string Location => _location ?? (_location = Assembly.GetEntryAssembly().Location);

        public static string Name => _name ?? (_name = Path.GetFileName(Location));

        public static string Directory => _directory ?? (_directory = Path.GetDirectoryName(Location));

        public static bool IsInDevelopment => string.Equals(Name, @"devenv.exe", StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(Name, @"XDesProc.exe", StringComparison.OrdinalIgnoreCase);

        private const long PackedSizeAtLeast = 2000000;

        public static bool IsPacked => new FileInfo(Location).Length > PackedSizeAtLeast;
    }
}
