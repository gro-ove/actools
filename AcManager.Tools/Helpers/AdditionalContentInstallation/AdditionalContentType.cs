using System.ComponentModel;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public enum AdditionalContentType {
        [Description("car")]
        Car,

        [Description("car skin")]
        CarSkin,

        [Description("track")]
        Track,

        [Description("showroom")]
        Showroom,

        [Description("font")]
        Font
    }
}