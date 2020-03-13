using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class PatchPluginEntry : ContentEntryBase {
        private readonly string _destination;
        private readonly List<string> _toInstall;

        public PatchPluginEntry([NotNull] string path, IEnumerable<string> items, string name, string destination, double priority = 10d,
                string version = null, string description = null)
                : base(path, "", name, version, null, description) {
            _destination = destination;
            _toInstall = items.ToList();
            Priority = priority;
        }

        public override double Priority { get; }

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => Name;
        public override string NewFormat => Name;
        public override string ExistingFormat => $"Update for {(Name.StartsWith("Weather FX") ? Name : Name.ToSentenceMember())}";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst, false);
            yield return new UpdateOption(ToolsStrings.Installator_UpdateEverything, false);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            var first = true;
            var cleanInstall = SelectedOption?.DisplayName == ToolsStrings.Installator_RemoveExistingFirst;
            return new CopyCallback(info => {
                var filename = info.Key;

                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)
                        || !_toInstall.Contains(info.Key) && !_toInstall.Any(x => FileUtils.IsAffectedBy(info.Key, x))) {
                    return null;
                }

                if (first) {
                    var directory = InstallTo();
                    if (Directory.Exists(directory) && cleanInstall) {
                        FileUtils.Recycle(directory);
                    }
                    first = false;
                }

                return Path.Combine(InstallTo(), FileUtils.GetRelativePath(filename, path));
            });
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(InstallTo());
        }

        private string InstallTo() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, _destination);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            if (FileUtils.Exists(InstallTo())) {
                return Task.Run(() => {
                    var cfg = new IniFile(Path.Combine(_destination, "manifest.ini"))["ABOUT"];
                    return Tuple.Create(cfg.GetNonEmpty("NAME", Name), cfg.GetNonEmpty("VERSION"));
                });
            }
            return Task.FromResult<Tuple<string, string>>(null);
        }
    }
}