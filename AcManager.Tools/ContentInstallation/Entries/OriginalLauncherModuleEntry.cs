using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class OriginalLauncherModuleEntry : ContentEntryBase {
        public OriginalLauncherModuleEntry([NotNull] string path, string id, string name, string version, byte[] iconData, string description)
                : base(path, id, name, version, iconData, description) {
            Priority = 10d;
        }

        public override double Priority { get; }

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => Name;
        public override string NewFormat => Name;
        public override string ExistingFormat => $"Update for original launcher module {Name}";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption(ToolsStrings.Installator_UpdateEverything, false);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            var first = true;
            return new CopyCallback(info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)) return null;

                if (first) {
                    var directory = InstallTo();
                    if (Directory.Exists(directory) ) {
                        FileUtils.Recycle(directory);
                    }
                    first = false;
                }

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return Path.Combine(destination, subFilename);
            });
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(InstallTo());
        }

        private string InstallTo() {
            return Path.Combine(Path.Combine(AcRootDirectory.Instance.RequireValue, @"launcher\themes\default\modules"), Id);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var existingManifest = Path.Combine(InstallTo(), "manifest.ini");
            if (File.Exists(InstallTo())) {
                var config = new IniFile(existingManifest)["MODULE"];
                return Task.FromResult(Tuple.Create(config.GetNonEmpty("NAME"), config.GetNonEmpty("VERSION")));
            }
            return Task.FromResult<Tuple<string, string>>(null);
        }
    }
}