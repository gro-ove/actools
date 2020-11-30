using System.ComponentModel;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Workshop.Data {
    public class WorkshopCommentsModel : NotifyPropertyChanged {
        public WorkshopCommentsGroupModel GroupModel { get; }

        private readonly string _url;

        public WorkshopCommentsModel(string url) {
            _url = url;
            GroupModel = new WorkshopCommentsGroupModel(url);
            GroupModel.Comments.ItemPropertyChanged += OnCommentPropertyChanged;
        }

        private WorkshopCommentsGroupModel _repliesGroupModel;

        public WorkshopCommentsGroupModel RepliesGroupModel {
            get => _repliesGroupModel;
            set => Apply(value, ref _repliesGroupModel);
        }

        private void OnCommentPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(WorkshopComment.ShowReplies) && sender is WorkshopComment comment && comment.ShowReplies) {
                ShowRepliesOf = comment;
            }
        }

        private DelegateCommand _backFromRepliesCommand;

        public DelegateCommand BackFromRepliesCommand => _backFromRepliesCommand
                ?? (_backFromRepliesCommand = new DelegateCommand(() => ShowRepliesOf = null));

        private WorkshopComment _showRepliesOf;

        public WorkshopComment ShowRepliesOf {
            get => _showRepliesOf;
            set {
                var oldValue = _showRepliesOf;
                Apply(value, ref _showRepliesOf, () => {
                    if (oldValue != null) {
                        oldValue.ShowReplies = false;
                        if (RepliesGroupModel != null) {
                            oldValue.Replies += RepliesGroupModel.NewCommentsAdded - RepliesGroupModel.CommentsDeleted;
                        }
                    }
                    if (_showRepliesOf != null) {
                        var repliesUrl = $"{_url.Replace("-comments/", "-comment-replies/")}/{_showRepliesOf.Id}";
                        RepliesGroupModel = new WorkshopCommentsGroupModel(repliesUrl);
                    } else {
                        RepliesGroupModel = null;
                    }
                });
            }
        }
    }
}