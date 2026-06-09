using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class TrueTypeFontContentEntry : ContentEntryBase<TrueTypeFontObject> {
        public override double Priority => 15d;

        public TrueTypeFontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(false, path, id, null, name, iconData: iconData) { }

        public override string GenericModTypeName => "TrueType font";
        public override string NewFormat => "New TrueType font “{0}”";
        public override string ExistingFormat => "Update for the TrueType font “{0}”";

        public override FileAcManager<TrueTypeFontObject> GetManager() {
            return TrueTypeFontsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything, false) };
        }
    }
}