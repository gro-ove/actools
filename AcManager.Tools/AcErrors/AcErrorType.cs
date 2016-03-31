using System;
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
        /// {0}: exception
        /// </summary>
        [Obsolete]
        [Description("Full loading unhandled error: {0}")]
        Load_Fully,


        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is missing")]
        Data_IniIsMissing,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [Obsolete]
        [Description("Can't read “{0}”")]
        Data_IniIsUnreadable,

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
        [Obsolete]
        [Description("Can't read “{0}”")]
        Data_JsonIsUnreadable,

        /// <summary>
        /// {0}: file name
        /// </summary>
        [Description("File “{0}” is damaged")]
        Data_JsonIsDamaged,
        
        [Description("Field with name is missing or empty")]
        Data_ObjectNameIsMissing,
        
        [Description("Field “brand” is missing or empty")]
        Data_CarBrandIsMissing,
        
        [Description("Directory “ui” is missing")]
        Data_UiDirectoryIsMissing,


        /// <summary>
        /// {0}: career name or id
        /// </summary>
        [Description("Events of “{0}” are missing")]
        Data_KunosCareerEventsAreMissing,

        [Description("Event conditions aren't supported")]
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
        [Description("Required skin “{1}” of {0} for is missing")]
        Data_KunosCareerCarSkinIsMissing,

        /// <summary>
        /// {0}: weather id
        /// </summary>
        [Description("Required weather “{0}” is missing")]
        Data_KunosCareerWeatherIsMissing,


        [Description("Car's parent is missing")]
        Car_ParentIsMissing,


        [Description("Skins directory is missing")]
        CarSkins_DirectoryIsMissing,

        [Description("Skins are missing")]
        CarSkins_SkinsAreMissing,

        [Description("Skins directory is unavailable")]
        CarSkins_DirectoryIsUnavailable,


        [Description("Model (kn5-file) is missing")]
        Showroom_Kn5IsMissing,
    }
}