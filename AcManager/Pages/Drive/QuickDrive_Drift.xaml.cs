using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Drift : IQuickDriveModeControl {
        public class ViewModel : QuickDriveSingleModeViewModel {
            public Game.StartType[] StartTypes => Game.StartType.Values;

            private Game.StartType _selectedStartType;

            public Game.StartType SelectedStartType {
                get => _selectedStartType;
                set {
                    if (Equals(value, _selectedStartType)) return;
                    _selectedStartType = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            #region Saveable
            protected new class SaveableData : QuickDriveSingleModeViewModel.SaveableData {
                public string StartType = Game.StartType.Pit.Id;
            }

            protected override ISaveHelper CreateSaveable(string key) {
                return new SaveHelper<SaveableData>(key, () => Save(new SaveableData()), Load);
            }

            protected SaveableData Save(SaveableData data) {
                base.Save(data);
                data.StartType = SelectedStartType.Id;
                return data;
            }

            protected void Load(SaveableData data) {
                base.Load(data);
                SelectedStartType = Game.StartType.Values.GetByIdOrDefault(data.StartType) ?? Game.StartType.Pit;
            }

            public ViewModel(bool initialize = true) {
                Initialize("__QuickDrive_Drift", initialize);
            }
            #endregion

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties,
                    string serializedQuickDrivePreset, IList<object> additionalProperties) {
                basicProperties.Ballast = PlayerBallast;
                basicProperties.Restrictor = PlayerRestrictor;
                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = new Game.DriftProperties {
                        Penalties = Penalties,
                        StartType = SelectedStartType
                    },
                    AdditionalPropertieses = additionalProperties.Concat(new object[] {
                        new QuickDrivePresetProperty(serializedQuickDrivePreset),
                        new CarCustomDataHelper(),
                        new CarExtendedPhysicsHelper(),
                    }).ToList()
                });
            }
        }


        public QuickDrive_Drift() {
            InitializeComponent();
        }

        public QuickDriveModeViewModel Model {
            get => (QuickDriveModeViewModel)DataContext;
            set => DataContext = value;
        }
    }
}
