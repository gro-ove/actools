using System.Threading.Tasks;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_TimeAttack : IQuickDriveModeControl {
        public class ViewModel : QuickDriveModeViewModel {
            private bool _penalties;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (Equals(value, _penalties)) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private class SaveableData {
                public bool Penalties;
            }

            public ViewModel(bool initialize = true) {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_TimeAttack", () => new SaveableData {
                    Penalties = Penalties,
                }, o => {
                    Penalties = o.Penalties;
                }, () => {
                    Penalties = true;
                });

                if (initialize) {
                    Saveable.Initialize();
                } else {
                    Saveable.Reset();
                }
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = TagRequired("circuit", track);
            }

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = new Game.TimeAttackProperties {
                        Penalties = Penalties
                    }
                });
            }
        }

        public QuickDrive_TimeAttack() {
            InitializeComponent();
        }


        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }
    }
}
