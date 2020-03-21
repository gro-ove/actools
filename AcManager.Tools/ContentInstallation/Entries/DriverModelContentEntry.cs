using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class DriverModelContentEntry : ContentEntryBase<DriverModelObject> {
        public override double Priority => 21d;

        public DriverModelContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "Driver model";
        public override string NewFormat => "New driver model “{0}”";
        public override string ExistingFormat => "Update for the driver model “{0}”";

        public override FileAcManager<DriverModelObject> GetManager() {
            return DriverModelsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything, false) };
        }
    }
}
