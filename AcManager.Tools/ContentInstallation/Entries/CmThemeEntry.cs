using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CmThemeEntry : ContentEntryBase {
        public CmThemeEntry([NotNull] string path, [NotNull] string id, string version)
                : base(true, path, id, null, AcStringValues.NameFromId(id.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase)), version) { }

        public override double Priority => 120d;

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => null;
        public override string NewFormat => "New CM theme {0}";
        public override string ExistingFormat => "Update for CM theme {0}";

        public static string GetVersion(string data, out bool isTheme) {
            var doc = XDocument.Parse(data);
            var n = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

            isTheme = doc.Root?.Name == n + "ResourceDictionary";
            if (!isTheme) return null;

            var nx = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var ns = XNamespace.Get("clr-namespace:System;assembly=mscorlib");
            return doc.Descendants(ns + "String")
                    .FirstOrDefault(x => x.Attribute(nx + "Key")?.Value == "Version")?.Value;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install", false);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var xaml = EntryPath;
            if (string.IsNullOrWhiteSpace(xaml)) return new CopyCallback(null);

            var resources = EntryPath.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase);
            return new CopyCallback(info => {
                var filename = info.Key;
                return FileUtils.ArePathsEqual(filename, xaml) ? Path.Combine(destination, Path.GetFileName(xaml))
                        : FileUtils.IsAffectedBy(filename, resources) ? Path.Combine(destination, FileUtils.GetRelativePath(filename, resources)) : null;
            });
        }

        protected override async Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var existing = Path.Combine(FilesStorage.Instance.GetDirectory("Themes"), Id);
            return File.Exists(existing) ? Tuple.Create(Name, GetVersion(await FileUtils.ReadAllTextAsync(existing), out var _)) : null;
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(FilesStorage.Instance.GetDirectory("Themes"));
        }
    }
}