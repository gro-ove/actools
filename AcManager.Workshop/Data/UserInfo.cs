using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class UserInfo : NotifyPropertyChanged {
        [JsonProperty("userID")]
        public string UserId { get; set; }

        [JsonProperty("isHidden")]
        public bool IsHidden { get; set; }

        [JsonProperty("isVirtual")]
        public bool IsVirtual { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("avatarImageSmall")]
        public string AvatarSmall { get; set; }

        [JsonProperty("avatarImageLarge")]
        public string AvatarLarge { get; set; }

        /*[JsonProperty("userURLs")]
        public Dictionary<string, string> UserUrls { get; set; }*/
    }
}