using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class WorkshopDownloadInformation : NotifyPropertyChanged {
        [JsonProperty("downloadURL")]
        public string DownloadUrl { get; set; }

        [JsonProperty("downloadSize")]
        public long DownloadSize { get; set; }
    }
}