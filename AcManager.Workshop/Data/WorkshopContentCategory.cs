using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class WorkshopContentCategory {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("uses")]
        public int Uses { get; set; }

        [JsonProperty("new")]
        public int NewItems { get; set; }

        [JsonIgnore]
        public bool HasNew => NewItems > 0;
    }
}