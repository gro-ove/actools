using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Drift : IQuickDriveModeControl {
        public class ViewModel : QuickDriveModeViewModel {
            private bool _penalties;
            private Game.StartType _selectedStartType;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public BindingList<Game.StartType> StartTypes => Game.StartType.Values;

            public Game.StartType SelectedStartType {
                get { return _selectedStartType; }
                set {
                    if (Equals(value, _selectedStartType)) return;
                    _selectedStartType = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private class SaveableData {
                public bool Penalties;
                public string StartType;
            }

            public ViewModel(bool initialize = true) {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Drift", () => new SaveableData {
                    Penalties = Penalties,
                    StartType = SelectedStartType.Value
                }, o => {
                    Penalties = o.Penalties;
                    SelectedStartType = Game.StartType.Values.FirstOrDefault(x => x.Value == o.StartType) ?? Game.StartType.Pit;
                }, () => {
                    Penalties = true;
                    SelectedStartType = Game.StartType.Pit;
                });

                if (initialize) {
                    Saveable.Initialize();
                } else {
                    Saveable.Reset();
                }
            }

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = new Game.DriftProperties {
                        Penalties = Penalties,
                        StartType = SelectedStartType
                    }
                });
            }
        }


        public QuickDrive_Drift() {
            InitializeComponent();
        }
        
        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }
    }
}
