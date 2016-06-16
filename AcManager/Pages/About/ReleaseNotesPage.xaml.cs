using System.Collections;
using System.Linq;
using System.Windows.Data;
using AcManager.About;
using AcManager.Internal;
using AcManager.Tools.About;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.About {
    /// <summary>
    /// Interaction logic for ReleaseNotesPage.xaml
    /// </summary>
    public partial class ReleaseNotesPage {
        private ReleaseNotesPageViewModel Model => (ReleaseNotesPageViewModel)DataContext;

        public ReleaseNotesPage() {
            DataContext = new ReleaseNotesPageViewModel();
            InitializeComponent();
        }

        public class ReleaseNotesPageViewModel : NotifyPropertyChanged, IComparer {
            public ReleaseNotesPageViewModel() {
                NotesList = new ListCollectionView(ReleaseNotes.Entries.Where(x => !x.IsLimited || AppKeyHolder.IsAllRight).ToList()) { CustomSort = this };
                NotesList.MoveCurrentToFirst();
            }

            private RelayCommand _markAllAsReadCommand;

            public RelayCommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new RelayCommand(o => {
                foreach (var note in ReleaseNotes.Entries.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, o => ReleaseNotes.Entries.Any(x => x.IsNew)));

            private ListCollectionView _notesList;

            public ListCollectionView NotesList {
                get { return _notesList; }
                set {
                    if (Equals(value, _notesList)) return;
                    _notesList = value;
                    OnPropertyChanged();
                }
            }

            public int Compare(object x, object y) {
                return -((PieceOfInformation)x).Version.CompareAsVersionTo(((PieceOfInformation)y).Version);
            }
        }
    }
}
