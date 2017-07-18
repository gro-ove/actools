using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Drag : IQuickDriveModeControl {
        public QuickDrive_Drag() {
            InitializeComponent();
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            ActualModel.Unload();
        }

        public QuickDriveModeViewModel Model {
            get { return ActualModel; }
            set { DataContext = value; }
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDrive_Race.ViewModel {
            protected override bool IgnoreStartingPosition => true;

            private int _matchesCount;

            public int MatchesCount {
                get { return _matchesCount; }
                set {
                    value = value.Clamp(0, 50);
                    if (Equals(value, _matchesCount)) return;
                    _matchesCount = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(bool initialize = true) : base(initialize) {}

            private new class SaveableData : QuickDrive_Race.ViewModel.SaveableData {
                public int? MatchesCount;
            }

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result) {
                base.Save(result);

                var r = (SaveableData)result;
                r.MatchesCount = MatchesCount;
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o) {
                base.Load(o);
                RaceGridViewModel.OpponentsNumber = 1;
                var r = (SaveableData)o;
                MatchesCount = r.MatchesCount ?? 10;
            }

            protected override void Reset() {
                base.Reset();
                MatchesCount = 10;
                RaceGridViewModel.OpponentsNumber = 1;
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = TagRequired("drag", track);
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Drag", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.DragProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    MatchesCount = MatchesCount,
                    BotCar = botCars.FirstOrDefault()
                };
            }
        }
    }
}
