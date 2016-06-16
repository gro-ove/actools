using System.Linq;
using AcManager.About;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public class AboutHelper : NotifyPropertyChanged {
        private bool _hasNewReleaseNotes;

        public bool HasNewReleaseNotes {
            get { return _hasNewReleaseNotes; }
            set {
                if (Equals(value, _hasNewReleaseNotes)) return;
                _hasNewReleaseNotes = value;
                OnPropertyChanged();
            }
        }

        private bool _hasNewImportantTips;

        public bool HasNewImportantTips {
            get { return _hasNewImportantTips; }
            set {
                if (Equals(value, _hasNewImportantTips)) return;
                _hasNewImportantTips = value;
                OnPropertyChanged();
            }
        }

        public AboutHelper() {
            CheckIfThereIsSomethingNew();
        }

        public void CheckIfThereIsSomethingNew () {
            HasNewReleaseNotes = ReleaseNotes.Entries.Any(x => x.IsNew);
            HasNewImportantTips = ImportantTips.Entries.Any(x => x.IsNew);
        }

        private static AboutHelper _instance;

        public static AboutHelper Instance => _instance ?? (_instance = new AboutHelper());
    }
}