using System.Collections.Generic;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;

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
            public ViewModel(bool initialize = true) : base(initialize) {}

            private new class SaveableData : QuickDrive_Race.ViewModel.SaveableData {}

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result) {
                base.Save(result);
                var r = (SaveableData)result;
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o) {
                base.Load(o);
                var r = (SaveableData)o;
            }

            protected override void Reset() {
                Penalties = false;
                AiLevelFixed = false;
                AiLevelArrangeRandomly = true;
                AiLevelArrangeReverse = false;
                AiLevel = 95;
                AiLevelMin = 85;
                OpponentsNumber = 12;
                SelectedGridType = GridType.SameCar;
                OpponentsCarsFilter = string.Empty;

                JumpStartPenalty = false;
                LapsNumber = 1;
                StartingPosition = 1;
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Trackday", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.TrackdayProperties {
                    AiLevel = AiLevelFixed ? AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = false,
                    StartingPosition = StartingPosition == 0 ? MathUtils.Random(1, OpponentsNumber + 2) : StartingPosition,
                    RaceLaps = LapsNumber,
                    BotCars = botCars
                };
            }
        }

        private void OpponentsCarsFilterTextBox_OnLostFocus(object sender, RoutedEventArgs e) {
            ((ViewModel)Model).AddOpponentsCarsFilter();
        }
    }
}
