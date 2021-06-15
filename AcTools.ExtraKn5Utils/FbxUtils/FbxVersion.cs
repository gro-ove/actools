// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

// ReSharper disable InconsistentNaming

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Enumerates the FBX file versions
    /// </summary>
    public enum FbxVersion {
        /// <summary>
        /// FBX version 6.0
        /// </summary>
        v6_0 = 6000,

        /// <summary>
        /// FBX version 6.1
        /// </summary>
        v6_1 = 6100,

        /// <summary>
        /// FBX version 7.0
        /// </summary>
        v7_0 = 7000,

        /// <summary>
        /// FBX 2011 version
        /// </summary>
        v7_1 = 7100,

        /// <summary>
        /// FBX 2012 version
        /// </summary>
        v7_2 = 7200,

        /// <summary>
        /// FBX 2013 version
        /// </summary>
        v7_3 = 7300,

        /// <summary>
        /// FBX 2014-15 version
        /// </summary>
        v7_4 = 7400,

        /// <summary>
        /// FBX 2016-19 version, adds large file (>2GB support), not compatible with older versions
        /// </summary>
        v7_5 = 7500
    }
}