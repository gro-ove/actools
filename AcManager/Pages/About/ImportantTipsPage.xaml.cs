using System;
using System.Linq;
using System.Windows.Data;
using AcManager.Tools.About;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.About {
    public partial class ImportantTipsPage : IParametrizedUriContent {
        private ImportantTipsPageViewModel Model => (ImportantTipsPageViewModel)DataContext;

        public void OnUri(Uri uri) {
            DataContext = new ImportantTipsPageViewModel(uri.GetQueryParam("Key"));
            InitializeComponent();
        }

        public class ImportantTipsPageViewModel : NotifyPropertyChanged {
            public ImportantTipsPageViewModel(string key) {
                NotesList = new ListCollectionView(ImportantTips.Tips.Reverse().ToList());
                if (key != null) {
                    NotesList.MoveCurrentTo(ImportantTips.Tips.FirstOrDefault(x => x.Key?.Contains(key) == true));
                } else {
                    NotesList.MoveCurrentToFirst();
                }
            }

            private RelayCommand _markAllAsReadCommand;

            public RelayCommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new RelayCommand(o => {
                foreach (var note in ImportantTips.Tips.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, o => ImportantTips.Tips.Any(x => x.IsNew)));

            private ListCollectionView _notesList;

            public ListCollectionView NotesList {
                get { return _notesList; }
                set {
                    if (Equals(value, _notesList)) return;
                    _notesList = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
