using System.Linq;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.About {
    public class AboutHelper : NotifyPropertyChanged {
        private bool _hasSomethingNew;

        public bool HasSomethingNew {
            get { return _hasSomethingNew; }
            set {
                if (Equals(value, _hasSomethingNew)) return;
                _hasSomethingNew = value;
                OnPropertyChanged();
            }
        }

        public AboutHelper() {
            CheckIfThereIsSomethingNew();
        }

        public void CheckIfThereIsSomethingNew () {
            HasSomethingNew = ReleaseNotes.Notes.Any(x => x.IsNew) || ImportantTips.Tips.Any(x => x.IsNew);
        }

        private static AboutHelper _instance;

        public static AboutHelper Instance => _instance ?? (_instance = new AboutHelper());
    }
}