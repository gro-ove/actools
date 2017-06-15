using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Windows {
    public static class Shell32 {
        [DllImport("shell32.dll")]
        public static extern int FindExecutable(string lpFile, string lpDirectory, [Out] StringBuilder lpResult);

        public static bool HasExecutable(string path){
            var executable = FindExecutable(path);
            return !string.IsNullOrEmpty(executable);
        }

        [CanBeNull]
        public static string FindExecutable(string path){
            var executable = new StringBuilder(1024);
            FindExecutable(path, string.Empty, executable);
            var result = executable.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}