using System.ComponentModel;

namespace AcManager.Tools.Objects {
    public enum ServerPresetAssistState {
        Denied = 0,
        Factory = 1,
        Forced = 2
    }

    public enum ServerPresetJumpStart {
        [LocalizedDescription("JumpStart_CarLocked")]
        CarLocked = 0,

        [LocalizedDescription("JumpStart_TeleportToPit")]
        TeleportToPit = 1,

        [LocalizedDescription("JumpStart_DriveThrough")]
        DriveThrough = 2
    }

    public enum ServerPresetRaceJoinType {
        [Description("Close")]
        Close,

        [Description("Open")]
        Open,

        [Description("Close at start")]
        CloseAtStart
    }
}