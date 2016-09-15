using System.Collections.Generic;
using System.Windows;
using AcManager.Tools.Helpers;
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

            private int _practiceDuration;

            public int PracticeDuration {
                get { return _practiceDuration; }
                set {
                    value = value.Clamp(0, 90);
                    if (Equals(value, _practiceDuration)) return;
                    _practiceDuration = value;
                    OnPropertyChanged();
                }
            }

            private int _qualificationDuration;

            public int QualificationDuration {
                get { return _qualificationDuration; }
                set {
                    value = value.Clamp(5, 90);
                    if (Equals(value, _qualificationDuration)) return;
                    _qualificationDuration = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(bool initialize = true) : base(initialize) {}

            private new class SaveableData : QuickDrive_Race.ViewModel.SaveableData {
                public int? PracticeLength, QualificationLength;
            }
            
            protected new class OldSaveableData : QuickDrive_Race.ViewModel.OldSaveableData {
                public int? PracticeLength, QualificationLength;
            }

            protected override void Save(QuickDrive_Race.ViewModel.SaveableData result) {
                base.Save(result);

                var r = (SaveableData)result;
                r.PracticeLength = PracticeDuration;
                r.QualificationLength = QualificationDuration;
            }

            protected override void Load(QuickDrive_Race.ViewModel.SaveableData o) {
                base.Load(o);

                var r = (SaveableData)o;
                PracticeDuration = r.PracticeLength ?? 15;
                QualificationDuration = r.QualificationLength ?? 30;
            }

            protected override void Load(QuickDrive_Race.ViewModel.OldSaveableData o) {
                base.Load(o);

                var r = (OldSaveableData)o;
                PracticeDuration = r.PracticeLength ?? 15;
                QualificationDuration = r.QualificationLength ?? 30;
            }

            protected override void Reset() {
                base.Reset();
                PracticeDuration = 15;
                QualificationDuration = 30;
            }

            protected override void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Weekend", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
                Saveable.RegisterUpgrade<OldSaveableData>(QuickDrive_Race.ViewModel.OldSaveableData.Test, Load);
            }

            protected override Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.WeekendProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = JumpStartPenalty,
                    StartingPosition = RaceGridViewModel.StartingPosition == 0
                            ? MathUtils.Random(1, RaceGridViewModel.OpponentsNumber + 2) : RaceGridViewModel.StartingPosition,
                    RaceLaps = LapsNumber,
                    BotCars = botCars,
                    PracticeDuration = PracticeDuration,
                    QualificationDuration = QualificationDuration
                };
            }
        }
    }
}
