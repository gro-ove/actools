using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CarContentEntry : ContentEntryBase<CarObject> {
        private readonly bool _isChild;

        public CarContentEntry([NotNull] string path, [NotNull] string id, bool isChild, string name = null, string version = null, byte[] iconData = null)
                : base(false, path, id, name, version, iconData) {
            _isChild = isChild;
        }

        public override double Priority => _isChild ? 50d : 51d;

        public override string GenericModTypeName => "Car";
        public override string NewFormat => ToolsStrings.ContentInstallation_CarNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_CarExisting;

        public override FileAcManager<CarObject> GetManager() {
            return CarsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (!x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json"));
            }

            bool PreviewsFilter(string x) {
                return !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation, false) { Filter = UiFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepSkinsPreviews, false) { Filter = PreviewsFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformationAndSkinsPreviews, false) { Filter = x => UiFilter(x) && PreviewsFilter(x) }
            });
        }
    }
}