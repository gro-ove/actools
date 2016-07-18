// ReSharper disable InconsistentNaming
namespace AcManager.Tools.AcErrors {
    public enum AcErrorType {
        /// <summary>
        /// {0}: exception
        /// </summary>
        [LocalizedDescription("LoadingUnhandledError")]
        Load_Base,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("FileIsMissing")]
        Data_IniIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("FileIsDamaged")]
        Data_IniIsDamaged,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("FileIsMissing")]
        Data_JsonIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription("FileIsDamaged")]
        Data_JsonIsDamaged,

        [LocalizedDescription("NameIsMissing")]
        Data_ObjectNameIsMissing,

        [LocalizedDescription("BrandNameIsMissing")]
        Data_CarBrandIsMissing,

        [LocalizedDescription("DirectoryUiIsMissing")]
        Data_UiDirectoryIsMissing,


        /// <summary>
        /// {0}: career name or id
        /// </summary>
        [LocalizedDescription("EventsOfAreMissing")]
        Data_KunosCareerEventsAreMissing,

        [LocalizedDescription("EventConditionsAreNotSupported")]
        Data_KunosCareerConditions,

        [LocalizedDescription("RequiredContentIsMissing")]
        Data_KunosCareerContentIsMissing,

        /// <summary>
        /// {0}: track id
        /// </summary>
        [LocalizedDescription("RequiredTrackIsMissing")]
        Data_KunosCareerTrackIsMissing,

        /// <summary>
        /// {0}: skin id
        /// </summary>
        [LocalizedDescription("RequiredCarIsMissing")]
        Data_KunosCareerCarIsMissing,

        /// <summary>
        /// {0}: car name or id
        /// {1}: skin id
        /// </summary>
        [LocalizedDescription("RequiredSkin1ForIsMissing")]
        Data_KunosCareerCarSkinIsMissing,

        /// <summary>
        /// {0}: weather id
        /// </summary>
        [LocalizedDescription("RequiredWeatherIsMissing")]
        Data_KunosCareerWeatherIsMissing,


        [LocalizedDescription("CarParentIsMissing")]
        Car_ParentIsMissing,

        [LocalizedDescription("BrandBadgeIsMissing")]
        Car_BrandBadgeIsMissing,

        [LocalizedDescription("UpgradeIconIsMissing")]
        Car_UpgradeIconIsMissing,


        [LocalizedDescription("SkinsAreMissing")]
        CarSkins_SkinsAreMissing,

        [LocalizedDescription("SkinsDirectoryIsUnavailable")]
        CarSkins_DirectoryIsUnavailable,


        [LocalizedDescription("SetupTrackIsMissing")]
        CarSetup_TrackIsMissing,


        [LocalizedDescription("SkinLiveryLiveryPngIsMissing")]
        CarSkin_LiveryIsMissing,

        [LocalizedDescription("SkinPreviewPreviewJpgIsMissing")]
        CarSkin_PreviewIsMissing,


        [LocalizedDescription("ModelKn5FileIsMissing")]
        Showroom_Kn5IsMissing,


        [LocalizedDescription("FontBitmapIsMissing")]
        Font_BitmapIsMissing,

        [LocalizedDescription("FontIsUsedButDisabled")]
        Font_UsedButDisabled,


        [LocalizedDescription("TrackIsMissing")]
        Replay_TrackIsMissing,

        [LocalizedDescription("NameShouldnTContainSymbolsLikeOr")]
        Replay_InvalidName,
    }
}