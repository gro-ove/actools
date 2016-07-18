// ReSharper disable InconsistentNaming

namespace AcManager.Tools.AcErrors {
    public enum AcErrorType {
        /// <summary>
        /// {0}: exception
        /// </summary>
        [LocalizedDescription("AcError_LoadingUnhandledError")]
        Load_Base,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("AcError_FileIsMissing")]
        Data_IniIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("AcError_FileIsDamaged")]
        Data_IniIsDamaged,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("AcError_FileIsMissing")]
        Data_JsonIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("AcError_FileIsDamaged")]
        Data_JsonIsDamaged,

        [LocalizedDescription("AcError_NameIsMissing")]
        Data_ObjectNameIsMissing,

        [LocalizedDescription("AcError_BrandNameIsMissing")]
        Data_CarBrandIsMissing,

        [LocalizedDescription("AcError_DirectoryUiIsMissing")]
        Data_UiDirectoryIsMissing,


        /// <summary>
        /// {0}: career name or id
        /// </summary>
        [LocalizedDescription("AcError_EventsOfAreMissing")]
        Data_KunosCareerEventsAreMissing,

        [LocalizedDescription("AcError_EventConditionsAreNotSupported")]
        Data_KunosCareerConditions,

        [LocalizedDescription("AcError_RequiredContentIsMissing")]
        Data_KunosCareerContentIsMissing,

        /// <summary>
        /// {0}: track id
        /// </summary>
        [LocalizedDescription("AcError_RequiredTrackIsMissing")]
        Data_KunosCareerTrackIsMissing,

        /// <summary>
        /// {0}: skin id
        /// </summary>
        [LocalizedDescription("AcError_RequiredCarIsMissing")]
        Data_KunosCareerCarIsMissing,

        /// <summary>
        /// {0}: car name or id
        /// {1}: skin id
        /// </summary>
        [LocalizedDescription("AcError_RequiredSkin1ForIsMissing")]
        Data_KunosCareerCarSkinIsMissing,

        /// <summary>
        /// {0}: weather id
        /// </summary>
        [LocalizedDescription("AcError_RequiredWeatherIsMissing")]
        Data_KunosCareerWeatherIsMissing,


        [LocalizedDescription("AcError_CarParentIsMissing")]
        Car_ParentIsMissing,

        [LocalizedDescription("AcError_BrandBadgeIsMissing")]
        Car_BrandBadgeIsMissing,

        [LocalizedDescription("AcError_UpgradeIconIsMissing")]
        Car_UpgradeIconIsMissing,


        [LocalizedDescription("AcError_SkinsAreMissing")]
        CarSkins_SkinsAreMissing,

        [LocalizedDescription("AcError_SkinsDirectoryIsUnavailable")]
        CarSkins_DirectoryIsUnavailable,


        [LocalizedDescription("AcError_SetupTrackIsMissing")]
        CarSetup_TrackIsMissing,


        [LocalizedDescription("AcError_SkinLiveryLiveryPngIsMissing")]
        CarSkin_LiveryIsMissing,

        [LocalizedDescription("AcError_SkinPreviewPreviewJpgIsMissing")]
        CarSkin_PreviewIsMissing,


        [LocalizedDescription("AcError_ModelKn5FileIsMissing")]
        Showroom_Kn5IsMissing,


        [LocalizedDescription("AcError_FontBitmapIsMissing")]
        Font_BitmapIsMissing,

        [LocalizedDescription("AcError_FontIsUsedButDisabled")]
        Font_UsedButDisabled,


        [LocalizedDescription("AcError_TrackIsMissing")]
        Replay_TrackIsMissing,

        [LocalizedDescription("AcError_NameShouldnTContainSymbolsLikeOr")]
        Replay_InvalidName,

        [LocalizedDescription("AcError_ColorCurvesIniIsMissing")]
        Weather_ColorCurvesIniIsMissing
    }
}