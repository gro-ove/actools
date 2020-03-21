using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    internal class SystemConfigEntry : ContentEntryBase {
        public override double Priority => 110d;

        private readonly string _destination;

        public SystemConfigEntry([NotNull] string path, [NotNull] string id, string name = null)
                : base(path, id, name ?? AcStringValues.NameFromId(id.ApartFromLast(".ini", StringComparison.OrdinalIgnoreCase))) {
            _destination = Path.Combine(AcPaths.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue), id);
        }

        protected sealed override bool GenericModSupportedByDesign => false;
        public override string GenericModTypeName => "AC config";
        public override string NewFormat => "New AC config {0}";
        public override string ExistingFormat => "Update for AC config {0}";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install", false);
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
