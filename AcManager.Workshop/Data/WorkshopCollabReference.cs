using AcManager.Workshop.Providers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class WorkshopCollabReference : NotifyPropertyChanged, IWithId {
        public WorkshopCollabReference() {
            UserInfo = Lazier.CreateAsync(() => UserInfoProvider.GetAsync(UserId));
        }

        [JsonIgnore]
        public Lazier<UserInfo> UserInfo { get; }

        [JsonProperty("userID")]
        public string UserId { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            OnPropertyChanged(nameof(DeleteCommand));
        }));

        string IWithId<string>.Id => UserId;
    }
}