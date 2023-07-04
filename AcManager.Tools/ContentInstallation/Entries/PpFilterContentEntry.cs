using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class PpFilterContentEntry : ContentEntryBase<PpFilterObject> {
        public override double Priority => 27d;

        public PpFilterContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(true, path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "PP-Filter";
        public override string NewFormat => "New PP-filter “{0}”";
        public override string ExistingFormat => "Update for the PP-filter “{0}”";

        public override FileAcManager<PpFilterObject> GetManager() {
            return PpFiltersManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything, false) };
        }
    }
}