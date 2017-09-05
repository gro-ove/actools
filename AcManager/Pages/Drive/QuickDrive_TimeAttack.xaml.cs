using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_TimeAttack : IQuickDriveModeControl {
        public class ViewModel : QuickDriveSingleModeViewModel {
            public ViewModel(bool initialize = true) {
                Initialize("__QuickDrive_TimeAttack", initialize);
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = TagRequired("circuit", track);
            }

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties,
                    string serializedQuickDrivePreset) {
                basicProperties.Ballast = PlayerBallast;
                basicProperties.Restrictor = PlayerRestrictor;
                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = new Game.TimeAttackProperties {
                        Penalties = Penalties
                    },
                    AdditionalPropertieses = {
                        new QuickDrivePresetProperty(serializedQuickDrivePreset)
                    }
                });
            }
        }

        public QuickDrive_TimeAttack() {
            InitializeComponent();
        }


        public QuickDriveModeViewModel Model {
            get => (QuickDriveModeViewModel)DataContext;
            set => DataContext = value;
        }
    }
}
