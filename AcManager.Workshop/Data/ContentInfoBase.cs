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

    public class ContentCollabsInfo : NotifyPropertyChanged {
        [JsonProperty("mainUserRole")]
        public string MainUserRole { get; set; }

        [JsonProperty("collabs")]
        public List<WorkshopCollabReference> CollabReferences { get; set; }
    }

    public abstract class ContentInfoBase : NotifyPropertyChanged {
        protected ContentInfoBase() {
            UserInfo = Lazier.CreateAsync(() => UserInfoProvider.GetAsync(UserId));
        }

        private WorkshopCommentsModel _comments;

        public WorkshopCommentsModel Comments => _comments ?? (_comments = new WorkshopCommentsModel(GetCommentsUrl()));

        protected abstract string GetCommentsUrl();

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

        [JsonProperty("collabsInfo")]
        public ContentCollabsInfo Collabs { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("originality")]
        public WorkshopOriginality Originality { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("informationURL")]
        public string Url { get; set; }

        [JsonProperty("downloadSize")]
        public long DownloadSize { get; set; }

        [JsonProperty("showroomSize")]
        public long ShowroomSize { get; set; }

        [JsonProperty("installSize")]
        public long SizeToInstall { get; set; }

        [JsonProperty("versions")]
        public List<VersionInfo> Versions { get; set; }

        public bool IsNew => DateTime.Now - ReleaseDate < SettingsHolder.Content.NewContentPeriod.TimeSpan;
    }
}