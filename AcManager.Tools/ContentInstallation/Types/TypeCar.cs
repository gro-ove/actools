using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeCar : ContentType {
        public TypeCar() : base(ToolsStrings.ContentInstallation_CarNew, ToolsStrings.ContentInstallation_CarExisting) {}

        public override IFileAcManager GetManager() {
            return CarsManager.Instance;
        }

        public override IEnumerable<UpdateOption> GetUpdateOptions() {
            Func<string, bool> uiFilter =
                    x => x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (
                            !x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json")
                            );
            Func<string, bool> previewsFilter =
                    x => !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = uiFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepSkinsPreviews) { Filter = previewsFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformationAndSkinsPreviews) { Filter = x => uiFilter(x) && previewsFilter(x) }
            });
        }
    }
}