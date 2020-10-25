using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class WorkshopContentCarSkin : ContentInfoBase {
        [JsonProperty("skinIcon")]
        public string LiveryImage { get; set; }

        [JsonProperty("previewImage")]
        public string PreviewImage { get; set; }

        [JsonProperty("driverName")]
        public string DriverName { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("isDefault")]
        public int IsDefaultNum { get; set; }

        [JsonIgnore] // TODO: custom serializer
        public bool IsDefault => IsDefaultNum == 1;
    }
}