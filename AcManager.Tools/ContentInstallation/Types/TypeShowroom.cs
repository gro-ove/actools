using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeShowroom : ContentType {
        public TypeShowroom() : base(ToolsStrings.ContentInstallation_ShowroomNew, ToolsStrings.ContentInstallation_ShowroomExisting) {}

        public override IEnumerable<UpdateOption> GetUpdateOptions() {
            Func<string, bool> uiFilter =
                x => x != @"ui\ui_showroom.json";
            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = uiFilter }
            });
        }

        public override IFileAcManager GetManager() {
            return ShowroomsManager.Instance;
        }
    }
}