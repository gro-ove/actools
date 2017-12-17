using System.ComponentModel;

namespace AcManager.Tools.ContentInstallation {
    public enum ContentInstallationEntryState {
        [Description("Loading")]
        Loading,

        [Description("Password required")]
        PasswordRequired,

        [Description("Waiting for confirmation")]
        WaitingForConfirmation,

        [Description("Finished")]
        Finished
    }
}