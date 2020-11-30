using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Workshop.Data {
    public class WorkshopCommentsGroupModel : NotifyPropertyChanged {
        private readonly string _url;
        private readonly string _likesUrl;
        private readonly string _likeIdsUrl;
        private readonly string _reportsUrl;

        public WorkshopCommentsGroupModel(string url) {
            _url = url;

            var commentsUrl = Regex.Match(url, "^(.+?)(ie)?s/([^/]+)");
            if (!commentsUrl.Success) throw new Exception("Unsupported comments URL: " + url);
            _likesUrl = $"{commentsUrl.Groups[1].Value}{(commentsUrl.Groups[2].Success ? "y" : "")}-likes/";
            _likeIdsUrl = $"{commentsUrl.Groups[1].Value}{(commentsUrl.Groups[2].Success ? "y" : "")}-like-ids/{commentsUrl.Groups[3].Value}";
            _reportsUrl = $"{commentsUrl.Groups[1].Value}{(commentsUrl.Groups[2].Success ? "y" : "")}-reports/";

            LoadAsync().Ignore();
            LoadLikeIdsAsync().Ignore();
            Comments.ItemPropertyChanged += OnCommentPropertyChanged;
        }

        private bool _innerChange;
        private readonly Busy _busyLike = new Busy();
        private readonly Busy _busyReport = new Busy();
        private readonly Busy _busyEdit = new Busy();

        private void OnLikeSet(WorkshopComment comment, bool likeSet) {
            if (_innerChange) return;
            if (_busyLike.Is) {
                _innerChange = true;
                comment.Liked = !likeSet;
                _innerChange = false;
            }
            _busyLike.Task(async () => {
                try {
                    if (likeSet) {
                        await WorkshopHolder.Client.PostAsync($"{_likesUrl}{comment.Id}");
                    } else {
                        await WorkshopHolder.Client.DeleteAsync($"{_likesUrl}{comment.Id}");
                    }
                } catch (WorkshopException e) when (e.Code == HttpStatusCode.Conflict || e.Code == HttpStatusCode.NotFound) {
                    // ignore such cases, as they usually mean desired task is already completed
                    _innerChange = true;
                    comment.Likes = likeSet ? comment.Likes - 1 : comment.Likes + 1;
                    _innerChange = false;
                } catch (Exception e) {
                    _innerChange = true;
                    comment.Liked = !likeSet;
                    _innerChange = false;
                    NonfatalError.Notify(likeSet ? "Failed to send a like" : "Failed to delete a like", e);
                }
            });
        }

        private void OnReport(WorkshopComment comment, string message) {
            _busyReport.Task(async () => {
                try {
                    await WorkshopHolder.Client.PostAsync($"{_reportsUrl}{comment.Id}", new { message });
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to send a report", e);
                    return;
                }
                try {
                    await WorkshopHolder.Client.GetAsync<WorkshopComment>($"{_url}/{comment.Id}");
                } catch (WorkshopException e) when (e.Code == HttpStatusCode.NotFound) {
                    Comments.Remove(comment);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            });
        }

        private void OnCommentChange(WorkshopComment comment, string newMessage) {
            _busyEdit.Task(async () => {
                try {
                    await WorkshopHolder.Client.PatchAsync($"{_url}/{comment.Id}", new { comment = newMessage });
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to edit a comment", e);
                    return;
                }
                try {
                    comment.Message = (await WorkshopHolder.Client.GetAsync<WorkshopComment>($"{_url}/{comment.Id}")).Message;
                } catch (Exception e) {
                    Logging.Warning(e);
                    comment.Message = newMessage;
                }
                comment.ChangeTo = null;
            });
        }

        private void OnCommentDelete(WorkshopComment comment) {
            _busyEdit.Task(async () => {
                try {
                    await WorkshopHolder.Client.DeleteAsync($"{_url}/{comment.Id}");
                    Comments.Remove(comment);
                    ++CommentsDeleted;
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to edit a comment", e);
                }
            });
        }

        private void OnCommentPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (sender is WorkshopComment comment) {
                if (args.PropertyName == nameof(WorkshopComment.Likes)) {
                    OnLikeSet(comment, comment.Liked);
                } else if (args.PropertyName == nameof(WorkshopComment.ReportWith) && !string.IsNullOrEmpty(comment.ReportWith)) {
                    OnReport(comment, comment.ReportWith);
                } else if (args.PropertyName == nameof(WorkshopComment.ChangeTo) && !string.IsNullOrEmpty(comment.ChangeTo)) {
                    OnCommentChange(comment, comment.ChangeTo);
                } else if (args.PropertyName == nameof(WorkshopComment.IsToDelete) && comment.IsToDelete) {
                    OnCommentDelete(comment);
                }
            }
        }

        private string _commentText;

        public string CommentText {
            get => _commentText;
            set => Apply(value, ref _commentText);
        }

        private bool _commentEditable = true;

        public bool CommentEditable {
            get => _commentEditable;
            set => Apply(value, ref _commentEditable);
        }

        private AsyncCommand _sendCommentCommand;

        public AsyncCommand SendCommentCommand => _sendCommentCommand ?? (_sendCommentCommand = new AsyncCommand(async () => {
            CommentEditable = false;
            var message = CommentText;
            try {
                await WorkshopHolder.Client.PostAsync(_url, new { comment = message });
            } catch (Exception e) {
                NonfatalError.Notify("Failed to post a comment", e);
            }
            CommentText = null;
            CommentEditable = true;
            await LoadNewCommentsAsync(new WorkshopComment {
                UserId = WorkshopHolder.Client.UserId,
                Message = message,
                DateTimestamp = DateTime.Now.ToMillisecondsTimestamp()
            });
        }, () => !string.IsNullOrWhiteSpace(CommentText) && WorkshopHolder.Model.AuthorizedAs?.IsVirtual == false));

        public ChangeableObservableCollection<WorkshopComment> Comments { get; } = new ChangeableObservableCollection<WorkshopComment>();

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            set => Apply(value, ref _isLoading);
        }

        private int _loadingPhase;
        private string _continuationToken;
        private WorkshopComment _lastComment;

        private string _lastError;

        public string LastError {
            get => _lastError;
            set => Apply(value, ref _lastError);
        }

        private long[] _likedIds;

        [CanBeNull]
        private WorkshopComment PrepareForAdding([NotNull] WorkshopComment comment) {
            if (Comments.GetByIdOrDefault(comment.Id) != null) return null;
            if (_likedIds?.ArrayContains(comment.Id) == true) {
                comment.MarkAsLiked();
            }
            return comment;
        }

        private async Task LoadAsync() {
            try {
                LastError = null;
                var loadingPhase = ++_loadingPhase;
                IsLoading = true;
                _continuationToken = null;
                var comments = await WorkshopHolder.Client.GetAsync<WorkshopComment[]>(
                        _continuationToken == null ? _url : $"{_url}?continuationToken={Uri.EscapeDataString(_continuationToken)}",
                        (status, headers) => {
                            if (loadingPhase != _loadingPhase || !headers.Contains("x-continuation-token")) return;
                            _continuationToken = headers.GetValues("x-continuation-token").FirstOrDefault();
                        });
                if (loadingPhase != _loadingPhase) return;
                Comments.AddRange(comments.Select(PrepareForAdding).NonNull());
                IsLoading = false;

                if (_continuationToken != null) {
                    _lastComment = Comments.LastOrDefault();
                    if (_lastComment != null) {
                        _lastComment.PropertyChanged += OnLastCommentPropertyChanged;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
                LastError = WorkshopHelperUtils.GetDisplayErrorMessage(e);
            }
        }

        private async Task LoadLikeIdsAsync() {
            try {
                _likedIds = await WorkshopHolder.Client.GetAsync<long[]>(_likeIdsUrl);
                foreach (var comment in Comments) {
                    if (_likedIds.ArrayContains(comment.Id)) {
                        comment.MarkAsLiked();
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private void OnLastCommentPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(WorkshopComment.HasBeenRead)) {
                if (_lastComment != null) {
                    _lastComment.PropertyChanged -= OnLastCommentPropertyChanged;
                }
                LoadAsync().Ignore();
            }
        }

        private int _newCommentsAdded;

        public int NewCommentsAdded {
            get => _newCommentsAdded;
            set => Apply(value, ref _newCommentsAdded);
        }

        private int _commentsDeleted;

        public int CommentsDeleted {
            get => _commentsDeleted;
            set => Apply(value, ref _commentsDeleted);
        }

        public async Task LoadNewCommentsAsync(WorkshopComment addAsFallback = null) {
            try {
                var lastComments = await WorkshopHolder.Client.GetAsync<WorkshopComment[]>(_url);
                var commentsToInsert = lastComments.Select(PrepareForAdding).NonNull().ToList();
                commentsToInsert.Reverse();
                NewCommentsAdded += commentsToInsert.Count;
                foreach (var comment in commentsToInsert) {
                    Comments.Insert(0, comment);
                    await Task.Delay(50);
                }
            } catch (Exception e) {
                Logging.Warning(e);
                if (addAsFallback != null) {
                    Comments.Insert(0, addAsFallback);
                    ++NewCommentsAdded;
                }
            }
        }
    }
}