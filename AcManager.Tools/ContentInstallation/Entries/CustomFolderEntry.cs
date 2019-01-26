using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CustomFolderEntry : ContentEntryBase {
        private readonly string _relativeDestination;
        private readonly List<string> _toInstall;

        public CustomFolderEntry([NotNull] string path, IEnumerable<string> items, string name, string relativeDestination, double priority = 10d)
                : base(path, "", name) {
            _relativeDestination = relativeDestination;
            _toInstall = items.ToList();
            Priority = priority;
        }

        public override double Priority { get; }

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => Name;
        public override string NewFormat => Name;
        public override string ExistingFormat => $"Update for {Name.ToSentenceMember()}";

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
            return Path.Combine(AcRootDirectory.Instance.RequireValue, _relativeDestination);
        }

        protected override Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            if (FileUtils.Exists(InstallTo())) {
                return Task.FromResult(Tuple.Create(Name, (string)null));
            }
            return Task.FromResult<Tuple<string, string>>(null);
        }
    }
}