using System;
using System.Collections.Generic;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    [Flags]
    public enum UserFlags {
        None = 0,
        Hidden = 1
    }

    public class UserInfo : NotifyPropertyChanged {
        [JsonProperty("userID")]
        public string Username { get; set; }

        [JsonProperty("flags")]
        public UserFlags Flags { get; set; }

        [JsonIgnore]
        public bool IsHidden {
            get => Flags.HasFlag(UserFlags.Hidden);
            set => Flags = (Flags & ~UserFlags.Hidden) | (value ? UserFlags.Hidden : UserFlags.None);
        }

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

        [JsonProperty("userURLs")]
        public Dictionary<string, string> UserUrls { get; set; }
    }
}