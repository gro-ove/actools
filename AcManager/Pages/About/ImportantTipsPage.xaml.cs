using System;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.About;
using AcManager.Internal;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.About {
    public partial class ImportantTipsPage : IParametrizedUriContent {
        private ViewModel Model => (ViewModel)DataContext;

        public void OnUri(Uri uri) {
            DataContext = new ViewModel(uri.GetQueryParam("Key"));
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel(string key) {
                NotesList = new ListCollectionView(
                        ImportantTips.Entries.Where(x => !x.IsLimited || InternalUtils.IsAllRight).OrderBy(x => x.DisplayName).ToList());
                if (key != null) {
                    NotesList.MoveCurrentTo(ImportantTips.Entries.FirstOrDefault(x => x.Id?.Contains(key) == true));
                } else {
                    NotesList.MoveCurrentToFirst();
                }
            }

            private ICommand _markAllAsReadCommand;

            public ICommand MarkAllAsReadCommand => _markAllAsReadCommand ?? (_markAllAsReadCommand = new DelegateCommand(() => {
                foreach (var note in ImportantTips.Entries.Where(x => x.IsNew)) {
                    note.MarkAsRead();
                }
            }, () => ImportantTips.Entries.Any(x => x.IsNew)));

            private ListCollectionView _notesList;

            public ListCollectionView NotesList {
                get => _notesList;
                set {
                    if (Equals(value, _notesList)) return;
                    _notesList = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}