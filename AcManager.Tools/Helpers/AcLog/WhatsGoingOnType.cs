using System.ComponentModel;

namespace AcManager.Tools.Helpers.AcLog {
    public enum WhatsGoingOnType {
        [LocalizedDescription("LogHelper_PasswordIsInvalid")]
        OnlineWrongPassword,

        [LocalizedDescription("LogHelper_CannotConnectToRemoteServer")]
        OnlineConnectionFailed,

        [LocalizedDescription("LogHelper_SuspensionIsMissing")]
        SuspensionIsMissing,

        [LocalizedDescription("LogHelper_WheelsAreMissing")]
        WheelsAreMissing,

        [LocalizedDescription("LogHelper_DriverIsMissing")]
        DriverModelIsMissing,

        [LocalizedDescription("LogHelper_TimeAttackNotSupported")]
        TimeAttackNotSupported,

        [LocalizedDescription("LogHelper_DefaultPpFilterIsMissing")]
        DefaultPpFilterIsMissing,

        [LocalizedDescription("LogHelper_PpFilterIsMissing")]
        PpFilterIsMissing,

        [LocalizedDescription("LogHelper_ShaderIsMissing")]
        ShaderIsMissing,

        [LocalizedDescription("LogHelper_CloudsMightBeMissing")]
        CloudsMightBeMissing,

        // TRANSLATE ME
        [Description("Game might be obsolete")]
        GameMightBeObsolete,

        [Description("Model is obsolete; please, consider updating flames to the second version")]
        FlamesV1TextureNotFound,

        [Description("Flames textures are missing")]
        FlamesFlashTexturesAreMissing
    }
}