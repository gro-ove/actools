using System;
using System.Linq;
using System.Windows.Data;
using AcManager.About;
using AcManager.Internal;
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
                NotesList = new ListCollectionView(ImportantTips.Entries.Where(x => !x.IsLimited || AppKeyHolder.IsAllRight).Reverse().ToList());
                if (key != null) {
                    NotesList.MoveCurrentTo(ImportantTips.Entries.FirstOrDefault(x => x.Id?.Contains(key) == true));
                } else {
                    NotesList.MoveCurrentToFirst();
                }
            }

            private RelayCommand _markAllAsReadCommand;

            public RelayCommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new RelayCommand(o => {
                foreach (var note in ImportantTips.Entries.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, o => ImportantTips.Entries.Any(x => x.IsNew)));

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
