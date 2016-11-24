using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Helpers {
    [Localizable(false)]
    public static class CefSharpResolverService {
        public static bool IsInitialized => _cefSharpPath != null;

        private const string DllExtension = ".dll";
        private static readonly string[] Dependencies;
        private static readonly string[] Resources;
        private static string _cefSharpPath;

        static CefSharpResolverService() {
            Dependencies = new[] {
                "cefsharp",
                "cefsharp.core",
                "cefsharp.wpf"
            };

            Resources = new[] {
                "cefsharp.resources",
                "cefsharp.core.resources",
                "cefsharp.wpf.resources"
            };
        }

        /// <summary>
        /// Initializes and activates the <see cref="CefSharpResolverService"/>.
        /// </summary>
        /// <param name="directoryPath">
        /// The path to the directory containing the CefSharp assemblies
        /// and its native references.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// A null reference or an empty string specified for <paramref name="directoryPath"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The directory specified does not exist or it does not contain the
        /// necessary CefSharp assemblies.
        /// </exception>
        public static void Initialize(string directoryPath) {
            if (string.IsNullOrEmpty(directoryPath)) {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            if (!directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                directoryPath += Path.DirectorySeparatorChar;
            }

            var dllPath = $"{directoryPath}{Dependencies[0]}{DllExtension}";
            if (!File.Exists(dllPath)) {
                throw new ArgumentException("The directory specified does not contain the CefSharp assemblies", nameof(directoryPath));
            }

            _cefSharpPath = directoryPath;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveCefSharp;
            Logging.Write(directoryPath);
        }

        /// <summary>
        /// Stops monitoring for requested CefSharp assemblies.
        /// </summary>
        public static void Stop() {
            _cefSharpPath = null;
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveCefSharp;
        }

        private static readonly Dictionary<string, Assembly> Cached = new Dictionary<string, Assembly>();

        private static Assembly ResolveCefSharp(object sender, ResolveEventArgs args) {
            try {
                var id = new AssemblyName(args.Name).Name.ToLower();

                Assembly result;
                if (Cached.TryGetValue(id, out result)) return result;

                if (Dependencies.Contains(id)) {
                    var dependencyPath = Path.Combine(_cefSharpPath, $"{id}{DllExtension}");
                    if (!File.Exists(dependencyPath)) {
                        Logging.Error($"CefSharp library not found: “{id}”");
                        return null;
                    }

                    var assembly = Assembly.LoadFrom(dependencyPath);
                    Cached[id] = assembly;
                    return assembly;
                }

                return null;
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }
    }
}