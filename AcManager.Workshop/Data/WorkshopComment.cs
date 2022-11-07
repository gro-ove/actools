using System;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Workshop.Providers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Workshop.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class WorkshopComment : NotifyPropertyChanged, IWithId<long> {
        public WorkshopComment() {
            UserInfo = Lazier.CreateAsync(() => UserInfoProvider.GetAsync(UserId));
        }

        [JsonProperty("userID")]
        public string UserId { get; set; }

        [JsonIgnore]
        public Lazier<UserInfo> UserInfo { get; }

        public bool IsOwn => UserId == WorkshopHolder.Client.UserId;

        public bool IsEditable => IsOwn && DateTime.Now - Date < TimeSpan.FromMinutes(30d) && Replies == 0d;
        public bool IsDeletable => IsOwn && DateTime.Now - Date < TimeSpan.FromMinutes(30d) && Likes == 0d && Replies == 0;

        private string _message;

        [JsonProperty("comment")]
        public string Message {
            get {
                HasBeenRead = true;
                return _message;
            }
            set => Apply(value, ref _message);
        }

        [JsonProperty("likes")]
        private int _likes;

        public int Likes {
            get => _likes;
            set => Apply(Math.Max(value, 0), ref _likes);
        }

        private bool _liked;

        public bool Liked {
            get => _liked;
            set => Apply(value, ref _liked, () => Likes += value ? 1 : -1);
        }

        public void MarkAsLiked() {
            if (_liked) return;
            _liked = true;
            OnPropertyChanged(nameof(Liked));
        }

        [JsonProperty("date")]
        public long DateTimestamp { get; set; }

        public DateTime Date => DateTimestamp.ToDateTimeFromMilliseconds();

        private bool _hasBeenRead;

        public bool HasBeenRead {
            get => _hasBeenRead;
            set => Apply(value, ref _hasBeenRead);
        }

        public long Id => DateTimestamp;

        [JsonProperty("replies")]
        public int Replies { get; set; }

        public ChangeableObservableCollection<WorkshopComment> Children { get; set; }

        private string _replyText;

        public string ReplyText {
            get => _replyText;
            set => Apply(value, ref _replyText);
        }

        private bool _showReplies;

        public bool ShowReplies {
            get => _showReplies;
            set => Apply(value, ref _showReplies);
        }

        private string _reportWith;

        public string ReportWith {
            get => _reportWith;
            set => Apply(value, ref _reportWith);
        }

        private string _changeTo;

        public string ChangeTo {
            get => _changeTo;
            set => Apply(value, ref _changeTo);
        }

        private bool _isToDelete;

        public bool IsToDelete {
            get => _isToDelete;
            set => Apply(value, ref _isToDelete);
        }

        private DelegateCommand _showRepliesCommand;

        public DelegateCommand ShowRepliesCommand => _showRepliesCommand ?? (_showRepliesCommand = new DelegateCommand(() => { ShowReplies = true; }));

        private AsyncCommand _reportCommand;

        public AsyncCommand ReportCommand => _reportCommand ?? (_reportCommand = new AsyncCommand(async () => {
            var reason = await Prompt.ShowAsync("What’s the problem?", "Report a comment", required: true, suggestions: new[] {
                "Spam",
                "Offensive",
                "Misleading",
            });
            if (reason != null) {
                ReportWith = reason;
                await Task.Delay(TimeSpan.FromSeconds(5d));
            }
        }, () => ReportWith == null));

        private AsyncCommand _editCommand;

        public AsyncCommand EditCommand => _editCommand ?? (_editCommand = new AsyncCommand(async () => {
            var newMessage = await Prompt.ShowAsync("New comment message:", "Edit comment", Message, required: true);
            if (newMessage != null) {
                ChangeTo = newMessage;
                await Task.Delay(TimeSpan.FromSeconds(5d));
            }
        }, () => IsEditable && ChangeTo == null));

        private AsyncCommand _deleteCommand;

        public AsyncCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new AsyncCommand(async () => {
            if (MessageDialog.Show("Are you sure to delete this comment? This action can’t be undone.", "Delete comment?", MessageDialogButton.YesNo)
                    == MessageBoxResult.Yes) {
                IsToDelete = true;
                await Task.Delay(TimeSpan.FromSeconds(5d));
            }
        }, () => IsDeletable && !IsToDelete));
    }
}