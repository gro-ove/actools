using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class ShowroomContentEntry : ContentEntryBase<ShowroomObject> {
        public override double Priority => 70d;

        public ShowroomContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string GenericModTypeName => "Showroom";
        public override string NewFormat => ToolsStrings.ContentInstallation_ShowroomNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_ShowroomExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui\ui_showroom.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation, false){ Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview", false) { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview", false) { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }

        public override FileAcManager<ShowroomObject> GetManager() {
            return ShowroomsManager.Instance;
        }
    }
}