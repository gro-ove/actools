using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    internal class TexturesConfigEntry : ContentEntryBase {
        public override double Priority => 110d;

        private readonly string _destination;

        public TexturesConfigEntry([NotNull] string path, [NotNull] string id, string name = null)
                : base(false, path, id, name ?? AcStringValues.NameFromId(id)) {
            _destination = Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "texture", id);
        }

        protected sealed override bool GenericModSupportedByDesign => IsNew;
        public override string GenericModTypeName => "Set of textures";
        public override string NewFormat => "New set of textures “{0}”";
        public override string ExistingFormat => "Update for the set of textures “{0}”";

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install", false);
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var path = EntryPath;
            return new CopyCallback(fileInfo => {
                var filename = fileInfo.Key;
                if (path != string.Empty && !FileUtils.IsAffectedBy(filename, path)) return null;

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
}