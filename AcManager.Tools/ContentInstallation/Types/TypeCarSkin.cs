using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeCarSkin : ContentType {
        public TypeCarSkin() : base(ToolsStrings.ContentInstallation_CarSkinNew, ToolsStrings.ContentInstallation_CarSkinExisting) { }

        public override IFileAcManager GetManager() {
            throw new NotImplementedException();
        }

        public override IEnumerable<UpdateOption> GetUpdateOptions() {
            Func<string, bool> uiFilter =
                    x => !x.StartsWith(@"ui\") ||
                            !x.EndsWith(@"\ui_track.json") && !x.EndsWith(@"\preview.png") &&
                                    !x.EndsWith(@"\outline.png");
            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = uiFilter }
            });
        }
    }
}