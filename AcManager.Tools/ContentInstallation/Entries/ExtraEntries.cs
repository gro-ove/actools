using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CmThemeEntry : ContentEntryBase {
        public CmThemeEntry([NotNull] string path, [NotNull] string id, string version)
                : base(path, id, AcStringValues.NameFromId(id.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase)), version) { }

        public override bool GenericModSupported => false;
        public override string GenericModTypeName => null;
        public override string NewFormat => "New CM theme {0}";
        public override string ExistingFormat => "Update for a CM theme {0}";

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
            yield return new UpdateOption("Install") {
                RemoveExisting = false
            };
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var xaml = EntryPath;
            if (string.IsNullOrWhiteSpace(xaml)) return new CopyCallback(null);

            var resources = EntryPath.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase);
            return new CopyCallback(info => {
                var filename = info.Key;
                return FileUtils.ArePathsEqual(filename, xaml) ? Path.Combine(destination, Path.GetFileName(xaml))
                        : FileUtils.IsAffected(resources, filename) ? Path.Combine(destination, FileUtils.GetRelativePath(filename, resources)) : null;
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

    internal class TexturesConfigEntry : ContentEntryBase {
        private readonly string _destination;

        public TexturesConfigEntry([NotNull] string path, [NotNull] string id, string name = null) : base(path, id, name ?? AcStringValues.NameFromId(id)) {
            _destination = Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "texture", id);
        }

        public override bool GenericModSupported => true;
        public override string GenericModTypeName => "Set Of Textures";
        public override string NewFormat => "New set of textures “{0}”";
        public override string ExistingFormat => "Update for the set of textures “{0}”";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install") {
                RemoveExisting = false
            };
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            return new CopyCallback(fileInfo => {
                var filename = fileInfo.Key;
                if (path != string.Empty && !FileUtils.IsAffected(path, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return Path.Combine(destination, subFilename);
            });
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(_destination);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            return Task.FromResult(Directory.Exists(_destination) ? Tuple.Create(Name, (string)null) : null);
        }
    }

    internal class SystemConfigEntry : ContentEntryBase {
        private readonly string _destination;

        public SystemConfigEntry([NotNull] string path, [NotNull] string id, string name = null)
                : base(path, id, name ?? AcStringValues.NameFromId(id.ApartFromLast(".ini", StringComparison.OrdinalIgnoreCase))) {
            _destination = Path.Combine(FileUtils.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue), id);
        }

        public override bool GenericModSupported => true;
        public override string GenericModTypeName => "AC Config";
        public override string NewFormat => "New AC config {0}";
        public override string ExistingFormat => "Update for a AC config {0}";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install") {
                RemoveExisting = false
            };
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            return string.IsNullOrWhiteSpace(path) ? new CopyCallback(null) :
                    new CopyCallback(info => FileUtils.ArePathsEqual(info.Key, path) ? _destination : null);
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(Path.GetDirectoryName(_destination));
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            return Task.FromResult(File.Exists(_destination) ? Tuple.Create(Name, (string)null) : null);
        }
    }
}
