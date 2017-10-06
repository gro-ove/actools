using System.ComponentModel;

namespace AcManager.Tools.Helpers.AcLog {
    public enum WhatsGoingOnType {
        [LocalizedDescription(nameof(ToolsStrings.LogHelper_PasswordIsInvalid))]
        OnlineWrongPassword,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_CannotConnectToRemoteServer))]
        OnlineConnectionFailed,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_SuspensionIsMissing))]
        SuspensionIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_WheelsAreMissing))]
        WheelsAreMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_DriverIsMissing))]
        DriverModelIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_TimeAttackNotSupported))]
        TimeAttackNotSupported,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_DefaultPpFilterIsMissing))]
        DefaultPpFilterIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_PpFilterIsMissing))]
        PpFilterIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_ShaderIsMissing))]
        ShaderIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.LogHelper_CloudsMightBeMissing))]
        CloudsMightBeMissing,

        // TRANSLATE ME
        [Description("App “{0}” might be broken")]
        AppMightBeBroken,

        [Description("Analog instruments of {0} might be broken")]
        AnalogInstrumentsAreDamaged,

        [Description("Steer animation might be missing")]
        SteerAnimIsMissing,

        [Description("Car {0} is missing")]
        CarIsMissing,

        [Description("Car {0} might be missing, or its data is heavily damaged")]
        CarIsMissingOrDamaged,

        [Description("Drivetrain of {0} might be broken")]
        DrivetrainIsDamaged,

        [Description("AI spline might be missing or broken")]
        AiSplineMissing,

        [Description("Game might be obsolete")]
        GameMightBeObsolete,

        [Description("Model is obsolete; please, consider updating flames to the second version")]
        FlamesV1TextureNotFound,

        [Description("Flames textures are missing")]
        FlamesFlashTexturesAreMissing,

        [Description("Default tyres index might be wrong")]
        DefaultTyresIndexMightBeWrong,

        [Description("Tyres might be wrong")]
        TyresMightBeWrong
    }
}