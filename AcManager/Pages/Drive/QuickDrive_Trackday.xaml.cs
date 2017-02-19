using System.Collections.Generic;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Trackday : IQuickDriveModeControl {
        public QuickDrive_Trackday() {
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

            public ViewModel(bool initialize = true) : base(initialize) {}

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Trackday", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
                Saveable.RegisterUpgrade<OldSaveableData>(OldSaveableData.Test, Load);
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = TagRequired("circuit", track);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.TrackdayProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = Game.JumpStartPenaltyType.None,
                    StartingPosition = 1,
                    RaceLaps = LapsNumber,
                    BotCars = botCars,
                    UsePracticeSessionType = SettingsHolder.Drive.QuickDriveTrackDayViaPractice
                };
            }
        }
    }
}
