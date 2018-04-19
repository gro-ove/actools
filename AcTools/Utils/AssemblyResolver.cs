using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public class AssemblyResolverErrorEventArgs : EventArgs {
        public AssemblyName AssemblyName { get; }
        public string Filename { get; }
        public Exception Exception { get; }
        public bool Handled { get; set; }

        public AssemblyResolverErrorEventArgs(AssemblyName assemblyName, string filename, Exception exception) {
            AssemblyName = assemblyName;
            Filename = filename;
            Exception = exception;
        }
    }

    public class AssemblyResolver {
        public bool IsInitialized => Directory != null;

        [CanBeNull]
        public string Directory { get; private set; }

        [NotNull]
        public Collection<string> Assemblies { get; } = new Collection<string>();

        [NotNull]
        public Collection<string> Imports { get; } = new Collection<string>();

        public bool RegisterDllDirectory { get; set; }

        public AssemblyResolver() {
            _filters = Lazier.Create(() => Assemblies.Select(RegexFromQuery.Create).ToArray());
        }

        private readonly Lazier<Regex[]> _filters;
        private readonly Dictionary<string, Assembly> _resolved = new Dictionary<string, Assembly>();

        public void Initialize([NotNull] string directory) {
            AcToolsLogging.Write(directory);

            if (string.IsNullOrEmpty(directory)) {
                throw new ArgumentNullException(nameof(directory));
            }

            Directory = directory;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            if (RegisterDllDirectory) {
                Kernel32.AddDllDirectory(directory);
            }

            foreach (var name in Imports) {
                Kernel32.LoadLibrary(Path.Combine(directory, name + DllExtension));
            }
        }

        public void Stop() {
            Directory = null;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        }

        private const string DllExtension = ".dll";

        private Assembly Load(AssemblyName name) {
            if (Directory == null) return null;

            var filename = Path.Combine(Directory, name.Name + DllExtension);
            try {
                FileUtils.Unblock(filename);
                return File.Exists(filename) ? Assembly.LoadFrom(filename) : null;
            } catch (Exception e) when (e.Message.Contains("0x80131515")) {
                throw new FileLoadException($"Can’t load locked binary {name.Name}; you can unlock it in file properties", e);
            } catch (Exception e) {
                var args = new AssemblyResolverErrorEventArgs(name, filename, e);
                Error?.Invoke(this, args);
                if (args.Handled) return null;
                throw;
            }
        }

        public event EventHandler<AssemblyResolverErrorEventArgs> Error;

        private Assembly Resolve(object sender, ResolveEventArgs args) {
            var name = new AssemblyName(args.Name);
            if (!_filters.RequireValue.Any(x => x.IsMatch(name.Name))) return null;

            if (!_resolved.TryGetValue(name.Name, out var result)) {
                _resolved[name.Name] = result = Load(name);
            }

            return result;
        }

        private static class RegexFromQuery {
            private static bool IsQuerySymbol(char c) {
                return c == '*' || c == '?';
            }

            private static bool IsRegexMetachar(char c) {
                return c >= 9 && c <= 10 || c >= 12 && c <= 13 || c == 32 || c >= 35 && c <= 36 || c >= 40 && c <= 43 || c == 46 || c == 63
                        || c >= 91 && c <= 92 ||
                        c == 94 || c >= 123 && c <= 124;
            }

            private static char ConvertBack(char c) {
                switch (c) {
                    case '\t':
                        return 't';
                    case '\n':
                        return 'n';
                    case '\f':
                        return 'f';
                    case '\r':
                        return 'r';
                    default:
                        return c;
                }
            }

            private static void AppendEscaped(char c, StringBuilder builder) {
                if (IsRegexMetachar(c)) {
                    builder.Append('\\');
                    builder.Append(ConvertBack(c));
                } else {
                    builder.Append(c);
                }
            }

            private static string PrepareQuery(string query) {
                var result = new StringBuilder((int)(query.Length * 1.2 + 2));
                result.Append(@"^");

                for (var i = 0; i < query.Length; i++) {
                    var c = query[i];

                    if (c == '\\') {
                        var n = i + 1 < query.Length ? query[i + 1] : (char)0;
                        if (IsQuerySymbol(n)) {
                            i++;
                            AppendEscaped(n, result);
                            continue;
                        }
                    }

                    if (c == '*') {
                        result.Append(".*");
                    } else if (c == '?') {
                        result.Append(".");
                    } else {
                        AppendEscaped(query[i], result);
                    }
                }

                result.Append(@"$");

                return result.ToString();
            }

            public static Regex Create(string query) {
                return new Regex(PrepareQuery(query), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }
    }
}