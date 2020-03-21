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
    internal class CrewBrandEntry : ContentEntryBase {
        public override double Priority => 109.6d;

        private readonly string _destination;

        public CrewBrandEntry([NotNull] string path, [NotNull] string id, string name = null) : base(path, id, GetName(id, name)) {
            _destination = Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "texture", "crew_brand", id);
        }

        private static string GetName(string id, string name) {
            return name ?? AcStringValues.NameFromId(id);
        }

        protected sealed override bool GenericModSupportedByDesign => IsNew;
        public override string GenericModTypeName => "Crew brand textures";
        public override string NewFormat => "New crew brand textures “{0}”";
        public override string ExistingFormat => "Update for the crew brand textures “{0}”";

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