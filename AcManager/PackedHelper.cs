using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

namespace AcManager {
    internal class PackedHelper {
        private readonly string _appId;
        private readonly string _referencesId;
        private readonly bool _logging;
        private readonly string _temporaryDirectory;
        private readonly ResourceManager _references;

        private List<string> _temporaryFiles;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Log(string s) {
            if (!_logging) return;

            const string filename = "packed_log.txt";
            if (s == null) {
                File.WriteAllBytes(filename, new byte[0]);
            } else {
                using (var writer = new StreamWriter(filename, true)) {
                    writer.WriteLine(DateTime.Now + ": " + s);
                }
            }
        }

        internal ResolveEventHandler Handler { get; }

        internal PackedHelper(string appId, string referencesId, bool logging) {
            _appId = appId;
            _referencesId = referencesId;
            _logging = logging;
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), appId + "_libs");
            Directory.CreateDirectory(_temporaryDirectory);

            Log(null);
            Handler = HandlerImpl;

            _references = new ResourceManager(referencesId, Assembly.GetExecutingAssembly());
        }
        
        private string ExtractResource(string id) {
            var hash = _references.GetString(id + "//hash");
            if (hash == null) {
                Log("missing!");
                return null;
            }

            var prefix = id + "_";
            var name = prefix + hash + ".dll";
            var filename = Path.Combine(_temporaryDirectory, name);
            Log("extracting resource: " + filename);

            if (File.Exists(filename)) {
                Log("already extracted, reusing");
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

        private Assembly HandlerImpl(object sender, ResolveEventArgs args) {
            var name = args.Name.Split(new[] { "," }, StringSplitOptions.None)[0];
            if (name.Contains(".resources")) {
                Log("skip: " + args.Name);
                return null;
            }

            Log("resolve: " + args.Name + " as " + name);

            try {
                var filename = ExtractResource(name);
                return filename == null ? null : Assembly.LoadFrom(filename);
            } catch (Exception e) {
                Log("error: " + e);
                return null;
            }
        }
    }
}