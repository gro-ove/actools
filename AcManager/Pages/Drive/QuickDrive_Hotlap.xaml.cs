using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Hotlap : IQuickDriveModeControl {
        public class QuickDrive_HotlapViewModel : QuickDriveModeViewModel {
            private bool _penalties, _ghostCar;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

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

            public QuickDrive_HotlapViewModel() {
                (Saveable = new SaveHelper<SaveableData>("__QuickDrive_Hotlap", () => new SaveableData {
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
                })).Init();
            }

            public override async Task Drive(CarObject selectedCar, TrackBaseObject selectedTrack, Game.AssistsProperties assistsProperties,
                Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                await StartAsync(new Game.StartProperties {
                    BasicProperties = new Game.BasicProperties {
                        CarId = selectedCar.Id,
                        CarSkinId = selectedCar.SelectedSkin?.Id,
                        TrackId = selectedTrack.Id,
                        TrackConfigurationId = selectedTrack.LayoutId
                    },
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
            DataContext = new QuickDrive_HotlapViewModel();
        }

        public QuickDriveModeViewModel Model => (QuickDrive_HotlapViewModel)DataContext;
    }
}
