using System.Linq;
using AcManager.About;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public class AboutHelper : NotifyPropertyChanged {
        private bool _hasNewReleaseNotes;

        public bool HasNewReleaseNotes {
            get { return _hasNewReleaseNotes; }
            set => Apply(value, ref _hasNewReleaseNotes);
        }

        private bool _hasNewImportantTips;

        public bool HasNewImportantTips {
            get { return _hasNewImportantTips; }
            set => Apply(value, ref _hasNewImportantTips);
        }

        public AboutHelper() {
            CheckIfThereIsSomethingNew();
        }

        public void CheckIfThereIsSomethingNew () {
            HasNewReleaseNotes = false;
            HasNewImportantTips = ImportantTips.Entries.Any(x => x.IsNew);
        }

        private static AboutHelper _instance;

        public static AboutHelper Instance => _instance ?? (_instance = new AboutHelper());
    }
}