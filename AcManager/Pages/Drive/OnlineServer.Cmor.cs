using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Commands;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer {
        public partial class ViewModel {
            public bool RatingAvailable => SettingsHolder.Online.UseCommunityRating && Entry.FromKunosList;

            private AsyncCommand _rateCommand;

            public AsyncCommand RateCommand => _rateCommand ?? (_rateCommand = new AsyncCommand(() => new ServerCommunityRating(Entry).ShowDialogAsync()));
        }
    }
}