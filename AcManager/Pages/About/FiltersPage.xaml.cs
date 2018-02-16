using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls.Helpers;
using AcManager.Tools.About;
using AcManager.Tools.Filters;
using AcManager.Tools.Filters.Testers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.About {
    public partial class FiltersPage {
        public FiltersPage() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private static string GetShortList_Line(KeywordDescription x) {
            return $" • [mono][b]{x.Key}[/b][/mono]: {x.Description.ToSentenceMember()} ({x.Type.GetReadableType().ToSentenceMember()}" +
                    (x.AlternativeKeys.Length > 0 ? $", alternative keys: {x.AlternativeKeys.Select(y => $"[mono][b]{y}[/b][/mono]").JoinToReadableString()}" : "") + ");";
        }

        public static string GetShortList(ITesterDescription descriptions) {
            var result = EnumExtension.GetValues<KeywordPriority>().OrderByDescending(x => (int)x).ToDictionary(x => x, x => new List<string>());
            foreach (var description in descriptions.GetDescriptions().DistinctBy(x => x.Key).OrderBy(x => x.Key)) {
                result[description.Priority].Add(GetShortList_Line(description));
            }
            return result.Where(x => x.Value.Count > 0).Select(x => x.Value.JoinToString('\n')).JoinToString("\n\n");
        }

        private static string GetHint(ITesterDescription descriptions) {
            return GetShortList(descriptions).ToSentence();
        }

        public class ViewModel : NotifyPropertyChanged, IComparer {
            private readonly PieceOfInformation[] _entries;

            public ViewModel() {
                _entries = new [] {
                    PieceOfInformation.Create("Properties for: Cars", GetHint(CarObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Tracks", GetHint(TrackObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Showrooms", GetHint(ShowroomObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Car Setups", GetHint(CarSetupObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Car Skins", GetHint(CarSkinObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Track Skins", GetHint(TrackSkinObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Weather", GetHint(WeatherObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Replays", GetHint(ReplayObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Screenshots", GetHint(FileTester.Instance)),
                    PieceOfInformation.Create("Properties for: Lap Times", GetHint(LapTimeTester.Instance)),
                    PieceOfInformation.Create("Properties for: Challenges", GetHint(SpecialEventObjectTester.Instance)),
                    PieceOfInformation.Create("Properties for: Online", GetHint(ServerEntryTester.Instance)),
                };

                NotesList = new ListCollectionView(Filtering.Entries.Concat(_entries).ToList()) { CustomSort = this };
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
                get => _notesList;
                set => Apply(value, ref _notesList);
            }

            public int Compare(object x, object y) {
                if (x == null) return y == null ? 0 : 1;
                if (y == null) return -1;

                var px = (PieceOfInformation)x;
                var py = (PieceOfInformation)y;

                if (px.Id == "filtersIntroduction") return -1;
                if (py.Id == "filtersIntroduction") return 1;
                return px.DisplayName.CompareAsVersionTo(py.DisplayName);
            }
        }
    }
}
