using System.ComponentModel;

namespace AcManager.Pages.ServerPreset {
    public enum ShareMode {
        [Description("None")]
        None,

        [Description("Download URL")]
        Url,

        [Description("Share from server")]
        Directly
    }
}