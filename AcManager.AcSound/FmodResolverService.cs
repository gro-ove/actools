using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AcManager.AcSound {
    [Localizable(false)]
    public static class FmodResolverService {
        public static bool IsInitialized => _pluginPath != null;

        private const string DllExtension = ".dll";
        private static readonly string[] Dependencies;
        private static string _pluginPath;

        static FmodResolverService() {
            Dependencies = new [] {
                "actools.soundbankplayer",
                "fmod.wrapper"
            };
        }

        public static void Initialize(string directoryPath) {
            if (string.IsNullOrEmpty(directoryPath)) {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            if (!directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                directoryPath += Path.DirectorySeparatorChar;
            }

            var dllPath = $"{directoryPath}{Dependencies[0]}{DllExtension}";
            if (!File.Exists(dllPath)) {
                throw new ArgumentException("The directory specified does not contain the Fmod plugin assemblies", nameof(directoryPath));
            }

            _pluginPath = directoryPath;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFmod;
        }

        public static void Stop() {
            _pluginPath = null;
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveFmod;
        }

        private static Assembly ResolveFmod(object sender, ResolveEventArgs args) {
            var unresolved = args.Name.ToLower();

            var dependencyDll = Dependencies.SingleOrDefault(item => unresolved.StartsWith(item));
            if (string.IsNullOrEmpty(dependencyDll)) return null;

            var dependencyPath = $"{_pluginPath}{Path.DirectorySeparatorChar}{dependencyDll}{DllExtension}";
            return File.Exists(dependencyPath) ? Assembly.LoadFrom(dependencyPath) : null;
        }
    }
}