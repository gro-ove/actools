using System.Collections;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.About;
using AcManager.Internal;
using AcManager.Tools.About;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.About {
    public partial class ReleaseNotesPage {
        public ReleaseNotesPage() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged, IComparer {
            public ViewModel() {
                NotesList = new ListCollectionView(ReleaseNotes.Entries.Where(x => !x.IsLimited || AppKeyHolder.IsAllRight).ToList()) { CustomSort = this };
                NotesList.MoveCurrentToFirst();
            }

            private ICommandExt _markAllAsReadCommand;

            public ICommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new DelegateCommand(() => {
                foreach (var note in ReleaseNotes.Entries.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, () => ReleaseNotes.Entries.Any(x => x.IsNew)));

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
