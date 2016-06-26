using System.ComponentModel;
// ReSharper disable InconsistentNaming

namespace AcManager.Tools.AcErrors {
    public enum AcErrorType {
        /// <summary>
        /// {0}: exception
        /// </summary>
        [Description("Loading unhandled error: {0}")]
        Load_Base,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is missing")]
        Data_IniIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is damaged")]
        Data_IniIsDamaged,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is missing")]
        Data_JsonIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is damaged")]
        Data_JsonIsDamaged,
        
        [Description("Name is missing")]
        Data_ObjectNameIsMissing,
        
        [Description("Brand name is missing")]
        Data_CarBrandIsMissing,
        
        [Description("Directory “ui” is missing")]
        Data_UiDirectoryIsMissing,


        /// <summary>
        /// {0}: career name or id
        /// </summary>
        [Description("Events of “{0}” are missing")]
        Data_KunosCareerEventsAreMissing,

        [Description("Event conditions aren’t supported")]
        Data_KunosCareerConditions,

        [Description("Required content is missing")]
        Data_KunosCareerContentIsMissing,

        /// <summary>
        /// {0}: track id
        /// </summary>
        [Description("Required track “{0}” is missing")]
        Data_KunosCareerTrackIsMissing,

        /// <summary>
        /// {0}: skin id
        /// </summary>
        [Description("Required car “{0}” is missing")]
        Data_KunosCareerCarIsMissing,

        /// <summary>
        /// {0}: car name or id
        /// {1}: skin id
        /// </summary>
        [Description("Required skin “{1}” for {0} is missing")]
        Data_KunosCareerCarSkinIsMissing,

        /// <summary>
        /// {0}: weather id
        /// </summary>
        [Description("Required weather “{0}” is missing")]
        Data_KunosCareerWeatherIsMissing,


        [Description("Car’s parent is missing")]
        Car_ParentIsMissing,

        [Description("Brand’s badge is missing")]
        Car_BrandBadgeIsMissing,

        [Description("Upgrade icon is missing")]
        Car_UpgradeIconIsMissing,
        

        [Description("Skins are missing")]
        CarSkins_SkinsAreMissing,

        [Description("Skins directory is unavailable")]
        CarSkins_DirectoryIsUnavailable,


        [Description("Setup’s track ({0}) is missing")]
        CarSetup_TrackIsMissing,


        [Description("Skin’s livery ({0}/livery.png) is missing")]
        CarSkin_LiveryIsMissing,

        [Description("Skin’s preview ({0}/preview.jpg) is missing")]
        CarSkin_PreviewIsMissing,


        [Description("Model (KN5 file) is missing")]
        Showroom_Kn5IsMissing,


        [Description("Font’s bitmap is missing")]
        Font_BitmapIsMissing,

        [Description("Font is used but disabled")]
        Font_UsedButDisabled,
    }
}