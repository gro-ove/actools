using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Hotlap : IQuickDriveModeControl {
        public class ViewModel : QuickDriveModeViewModel {
            private bool _penalties;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ghostCar;

            public bool GhostCar {
                get { return _ghostCar; }
                set {
                    if (value == _ghostCar) return;
                    _ghostCar = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _ghostCarAdvantage;

            public double GhostCarAdvantage {
                get { return _ghostCarAdvantage; }
                set {
                    if (Equals(value, _ghostCarAdvantage)) return;
                    _ghostCarAdvantage = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private class SaveableData {
                public bool Penalties, GhostCar;
                public double GhostCarAdvantage;
            }

            public ViewModel(bool initialize = true) {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Hotlap", () => new SaveableData {
                    Penalties = Penalties,
                    GhostCar = GhostCar,
                    GhostCarAdvantage = GhostCarAdvantage
                }, o => {
                    Penalties = o.Penalties;
                    GhostCar = o.GhostCar;
                    GhostCarAdvantage = o.GhostCarAdvantage;
                }, () => {
                    Penalties = true;
                    GhostCar = true;
                    GhostCarAdvantage = 0.0;
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
                    ModeProperties = new Game.HotlapProperties {
                        Penalties = Penalties,
                        GhostCar = GhostCar,
                        GhostCarAdvantage = GhostCarAdvantage
                    }
                });
            }
        }

        public QuickDrive_Hotlap() {
            InitializeComponent();
        }

        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }
    }
}
