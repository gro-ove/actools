// #define LOCALIZABLE

using System;
using System.Collections.Generic;
using System.ComponentModel;
#if LOCALIZABLE
using System.Globalization;
#endif
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Security.Permissions;

namespace AcManager {
    [Localizable(false)]
    internal class PackedHelper {
        private readonly string _logFilename;
        private readonly string _temporaryDirectory;
        private readonly ResourceManager _references;

        private List<string> _temporaryFiles;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Log(string s) {
            if (_logFilename == null) return;
            
            if (s == null) {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilename) ?? "");
                File.WriteAllBytes(_logFilename, new byte[0]);
            } else {
                using (var writer = new StreamWriter(_logFilename, true)) {
                    writer.WriteLine(s);
                }
            }
        }

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public void SetUnhandledExceptionHandler() {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e) {
            Log("unhandled exception: " + e.ExceptionObject);
            Environment.Exit(1);
        }

        internal ResolveEventHandler Handler { get; }

        internal PackedHelper(string appId, string referencesId, string logFilename) {
            _logFilename = logFilename;
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), appId + "_libs");
            Directory.CreateDirectory(_temporaryDirectory);

            Log(null);
            Handler = HandlerImpl;

            _references = new ResourceManager(referencesId, Assembly.GetExecutingAssembly());

            if (logFilename != null) {
                SetUnhandledExceptionHandler();
            }
        }
        
        private string ExtractResource(string id) {
            var hash = _references.GetString(id + "//hash");
            if (hash == null) {
                Log("missing!");
                return null;
            }

#if LOCALIZABLE
            _first = false;
#endif

            var prefix = id + "_";
            var name = prefix + hash + ".dll";
            var filename = Path.Combine(_temporaryDirectory, name);
            Log("extracting resource: " + filename);

            if (File.Exists(filename)) {
                Log("already extracted, reusing: " + filename);
                return filename;
            }

            var bytes = _references.GetObject(id) as byte[];
            if (bytes == null) {
                Log("missing!");
                return null;
            }

            var compressed = _references.GetObject(id + "//compressed") as bool?;
            if (compressed == true) {
                Log("compressed! decompressing…");

                using (var memory = new MemoryStream(bytes))
                using (var output = new MemoryStream()) {
                    using (var decomp = new DeflateStream(memory, CompressionMode.Decompress)) {
                        decomp.CopyTo(output);
                    }

                    bytes = output.ToArray();
                }
            }

            if (_temporaryFiles == null) {
                _temporaryFiles = Directory.GetFiles(_temporaryDirectory, "*.dll").Select(Path.GetFileName).ToList();
            }

            var previous = _temporaryFiles.FirstOrDefault(x => x.StartsWith(prefix));
            if (previous != null) {
                Log("removing previous version: " + previous);
                try {
                    File.Delete(Path.Combine(_temporaryDirectory, previous));
                    _temporaryFiles.Remove(previous);
                } catch (Exception e) {
                    Log("can’t remove: " + e);
                }
            }

            Log("writing, " + bytes.Length + " bytes");
            File.WriteAllBytes(filename, bytes);
            return filename;
        }

        private bool _ignore;

#if LOCALIZABLE
        private bool _first = true;
#endif

        private Assembly HandlerImpl(object sender, ResolveEventArgs args) {
            if (_ignore) return null;

            var splitted = args.Name.Split(',');
            var name = splitted[0];

#if LOCALIZABLE
            if (name == "Content Manager.resources" && _first) {
                Log(">> Content Manager.resources <<");
                return null;
            }

            if (name.EndsWith(".resources")) {
                var culture = splitted.ElementAtOrDefault(2)?.Split(new[] { "Culture=" }, StringSplitOptions.None).ElementAtOrDefault(1);
                Log("culture: " + culture);
                if (culture == "neutral") return null;

                var resourceId = CultureInfo.CurrentUICulture.IetfLanguageTag;
                if (!string.Equals(resourceId, "en-US", StringComparison.OrdinalIgnoreCase)) {
                    name = name.Replace(".resources", "." + resourceId);
                    Log("localized: " + name);
                } else {
                    Log("skip: " + args.Name);
                    return null;
                }
            }
#else
            if (name.EndsWith(".resources")) return null;
#endif

            if (name == "Magick.NET-x86") {
                return null;
            }

            Log("resolve: " + args.Name + " as " + name);

            try {
                _ignore = true;
                var filename = ExtractResource(name);
                return filename == null ? null : Assembly.LoadFrom(filename);
            } catch (Exception e) {
                Log("error: " + e);
                return null;
            } finally {
                _ignore = false;
            }
        }
    }
}