using System.ComponentModel;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public enum AdditionalContentType {
        [LocalizedDescription("AdditionalContent_Car")]
        Car,

        [LocalizedDescription("AdditionalContent_CarSkin")]
        CarSkin,

        [LocalizedDescription("AdditionalContent_Track")]
        Track,

        [LocalizedDescription("AdditionalContent_Showroom")]
        Showroom,

        [LocalizedDescription("AdditionalContent_Font")]
        Font,

        [LocalizedDescription("AdditionalContent_Weather")]
        Weather
    }
}