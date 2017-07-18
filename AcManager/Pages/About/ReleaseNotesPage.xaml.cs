using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.About;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.About {
    public partial class ReleaseNotesPage : ILoadableContent {
        private IReadOnlyList<ChangelogEntry> _changelog;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _changelog = await Task.Run(() => InternalUtils.LoadChangelog(CmApiProvider.UserAgent, false));
        }

        public void Load() {
            _changelog = InternalUtils.LoadChangelog(CmApiProvider.UserAgent, false);
        }

        public void Initialize() {
            DataContext = new ViewModel(_changelog);
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged, IComparer {
            private readonly PieceOfInformation[] _entries;

            public ViewModel(IReadOnlyList<ChangelogEntry> changelog) {
                _entries = changelog.Select(x => PieceOfInformation.Create($".chlg.{x.Version}", x.Version,
                        x.Date.ToString(CultureInfo.CurrentUICulture), x.Version, null, x.Changes, false, false)).ToArray();
                NotesList = new ListCollectionView(_entries) { CustomSort = this };
                NotesList.MoveCurrentToFirst();
            }

            private CommandBase _markAllAsReadCommand;

            public ICommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new DelegateCommand(() => {
                foreach (var note in _entries.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, () => _entries.Any(x => x.IsNew)));

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
