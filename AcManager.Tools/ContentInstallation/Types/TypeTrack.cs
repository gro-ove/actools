using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeTrack : ContentType {
        public TypeTrack() : base(ToolsStrings.ContentInstallation_TrackNew, ToolsStrings.ContentInstallation_TrackExisting) {}

        public override IEnumerable<UpdateOption> GetUpdateOptions() {
            Func<string, bool> uiFilter = x => x != @"ui_skin.json";
            Func<string, bool> previewFilter = x => x != @"preview.jpg";

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = uiFilter },
                new UpdateOption(ToolsStrings.Installator_KeepSkinPreview){ Filter = previewFilter },
                new UpdateOption(ToolsStrings.Installator_KeepUiInformationAndSkinPreview){ Filter = x => uiFilter(x) && previewFilter(x) }
            });
        }

        public override IFileAcManager GetManager() {
            return TracksManager.Instance;
        }
    }
}