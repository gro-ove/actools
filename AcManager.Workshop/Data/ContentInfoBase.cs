using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AcManager.Tools.Helpers;
using AcManager.Workshop.Providers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    public class VersionInfo : NotifyPropertyChanged {
        [JsonProperty("date")]
        public long Timestamp { get; set; }

        [JsonIgnore]
        public DateTime Date => Timestamp.ToDateTimeFromMilliseconds();

        [JsonIgnore]
        public string DisplayDate => Date.ToShortDateString();

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("changelog")]
        public string Changelog { get; set; }
    }

    public class ContentInfoBase : NotifyPropertyChanged {
        public ContentInfoBase() {
            UserInfo = Lazier.CreateAsync(() => UserInfoProvider.GetAsync(UserId));
        }

        [JsonProperty("entryID")]
        public string Id { get; set; }

        [JsonProperty("userID")]
        public string UserId { get; set; }

        [JsonIgnore]
        public Lazier<UserInfo> UserInfo { get; }

        [JsonProperty("releaseDate")]
        public long ReleaseTimestamp { get; set; }

        [JsonIgnore]
        public DateTime ReleaseDate => ReleaseTimestamp.ToDateTimeFromMilliseconds();

        [JsonIgnore]
        public string DisplayReleaseDate => ReleaseDate.ToShortDateString();

        [JsonProperty("lastDate")]
        public long LastTimestamp{ get; set; }

        [JsonIgnore]
        public DateTime LastDate => LastTimestamp.ToDateTimeFromMilliseconds();

        [JsonIgnore]
        public string DisplayLastDate => LastDate.ToShortDateString();

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonIgnore]
        public virtual string DisplayName => Name;

        [JsonProperty("tags")]
        public ObservableCollection<string> Tags { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("originality")]
        public WorkshopOriginality Originality { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("sizeFull")]
        public long SizeFull { get; set; }

        public long SizeToInstall => SizeFull != 0 ? SizeFull : Size;

        [JsonProperty("versions")]
        public List<VersionInfo> Versions { get; set; }

        public bool IsNew => DateTime.Now - ReleaseDate < SettingsHolder.Content.NewContentPeriod.TimeSpan;
    }
}