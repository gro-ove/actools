// ReSharper disable InconsistentNaming

namespace AcManager.Tools.AcErrors {
    public enum AcErrorType {
        /// <summary>
        /// {0}: exception
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_LoadingUnhandledError))]
        Load_Base,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_FileIsMissing))]
        Data_IniIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_FileIsDamaged))]
        Data_IniIsDamaged,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_FileIsMissing))]
        Data_JsonIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_FileIsDamaged))]
        Data_JsonIsDamaged,

        [LocalizedDescription(nameof(ToolsStrings.AcError_NameIsMissing))]
        Data_ObjectNameIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_BrandNameIsMissing))]
        Data_CarBrandIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_DirectoryUiIsMissing))]
        Data_UiDirectoryIsMissing,


        /// <summary>
        /// {0}: career name or id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_EventsOfAreMissing))]
        Data_KunosCareerEventsAreMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_EventConditionsAreNotSupported))]
        Data_KunosCareerConditions,

        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredContentIsMissing))]
        Data_KunosCareerContentIsMissing,

        /// <summary>
        /// {0}: track id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredTrackIsMissing))]
        Data_KunosCareerTrackIsMissing,

        /// <summary>
        /// {0}: car id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredCarIsMissing)), SeveralAllowed]
        Data_KunosCareerCarIsMissing,

        /// <summary>
        /// {0}: car name or id
        /// {1}: skin id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredSkinForIsMissing)), SeveralAllowed]
        Data_KunosCareerCarSkinIsMissing,

        /// <summary>
        /// {0}: weather id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredWeatherIsMissing))]
        Data_KunosCareerWeatherIsMissing,

        /// <summary>
        /// {0}: car id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredCarIsMissing)), SeveralAllowed]
        Data_UserChampionshipCarIsMissing,

        /// <summary>
        /// {0}: track id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredTrackIsMissing)), SeveralAllowed]
        Data_UserChampionshipTrackIsMissing,

        /// <summary>
        /// {0}: car name or id
        /// {1}: skin id
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_RequiredSkinForIsMissing)), SeveralAllowed]
        Data_UserChampionshipCarSkinIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [LocalizedDescription(nameof(ToolsStrings.AcError_FileIsDamaged))]
        ExtendedData_JsonIsDamaged,


        [LocalizedDescription(nameof(ToolsStrings.AcError_CarParentIsMissing))]
        Car_ParentIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_BrandBadgeIsMissing))]
        Car_BrandBadgeIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_UpgradeIconIsMissing))]
        Car_UpgradeIconIsMissing,


        [LocalizedDescription(nameof(ToolsStrings.AcError_SkinsAreMissing))]
        CarSkins_SkinsAreMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_SkinsDirectoryIsUnavailable))]
        CarSkins_DirectoryIsUnavailable,


        [LocalizedDescription(nameof(ToolsStrings.AcError_SetupTrackIsMissing))]
        CarSetup_TrackIsMissing,


        [LocalizedDescription(nameof(ToolsStrings.AcError_SkinLiveryLiveryPngIsMissing))]
        CarSkin_LiveryIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_SkinPreviewPreviewJpgIsMissing))]
        CarSkin_PreviewIsMissing,


        [LocalizedDescription(nameof(ToolsStrings.AcError_ModelKn5FileIsMissing))]
        Showroom_Kn5IsMissing,


        [LocalizedDescription(nameof(ToolsStrings.AcError_FontBitmapIsMissing))]
        Font_BitmapIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_FontIsUsedButDisabled))]
        Font_UsedButDisabled,


        [LocalizedDescription(nameof(ToolsStrings.AcError_TrackIsMissing))]
        Replay_TrackIsMissing,

        [LocalizedDescription(nameof(ToolsStrings.AcError_NameShouldnTContainSymbolsLikeOr))]
        Replay_InvalidName,

        [LocalizedDescription(nameof(ToolsStrings.AcError_ColorCurvesIniIsMissing))]
        Weather_ColorCurvesIniIsMissing
    }
}