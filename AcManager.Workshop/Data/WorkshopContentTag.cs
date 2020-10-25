using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class WorkshopContentTag {
        [JsonProperty("tagID")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("uses")]
        public int Uses { get; set; }

        [JsonProperty("new")]
        public int NewItems { get; set; }

        [JsonIgnore]
        public bool HasNew => NewItems > 0;

        [JsonIgnore]
        public bool Accented => Name?.StartsWith(@"#") == true;
    }
}