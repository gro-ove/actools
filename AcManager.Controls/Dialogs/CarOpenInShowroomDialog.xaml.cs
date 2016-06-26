using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.Dialogs {
    public partial class CarOpenInShowroomDialog {
        public const string UserPresetableKeyValue = "Showroom";

        public class CarOpenInShowroomDialogViewModel : NotifyPropertyChanged, IUserPresetable {
            private class SaveableData {
                public string ShowroomId, FilterId;
                public double CameraFov;
                public bool DisableSweetFx, DisableWatermark;
            }

            private void SaveLater() {
                _saveable.SaveLater();
                Changed?.Invoke(this, new EventArgs());
            }

            private readonly ISaveHelper _saveable;

            public CarOpenInShowroomDialogViewModel(string serializedPreset, CarObject carObject, string selectedSkinId) {
                ShowroomsManager.Instance.EnsureLoaded();
                PpFiltersManager.Instance.EnsureLoaded();

                SelectedCar = carObject;
                SelectedSkinId = selectedSkinId ?? SelectedCar.SelectedSkin?.Id;

                _saveable = new SaveHelper<SaveableData>("__CarOpenInShowroom", () => new SaveableData {
                    ShowroomId = SelectedShowroom?.Id,
                    FilterId = SelectedFilter?.Id,
                    CameraFov = CameraFov,
                    DisableSweetFx = DisableSweetFx,
                    DisableWatermark = DisableWatermark,
                }, o => {
                    if (o.ShowroomId != null) SelectedShowroom = ShowroomsManager.Instance.GetById(o.ShowroomId) ?? SelectedShowroom;
                    if (o.FilterId != null) SelectedFilter = PpFiltersManager.Instance.GetById(o.FilterId) ?? SelectedFilter;

                    CameraFov = o.CameraFov;
                    DisableWatermark = o.DisableWatermark;
                    DisableSweetFx = o.DisableSweetFx;
                }, () => {
                    SelectedShowroom = ShowroomsManager.Instance.GetDefault();
                    SelectedFilter = PpFiltersManager.Instance.GetDefault();
                    CameraFov = 30;
                    DisableWatermark = false;
                    DisableSweetFx = false;
                });

                if (string.IsNullOrEmpty(serializedPreset)) {
                    _saveable.Initialize();
                } else {
                    _saveable.Reset();
                    _saveable.FromSerializedString(serializedPreset);
                }
            }

            public CarOpenInShowroomDialogViewModel(CarObject carObject, string selectedSkinId) : this(null, carObject, selectedSkinId) {
            }

            public CarObject SelectedCar { get; set; }

            [CanBeNull]
            public string SelectedSkinId { get; set; }

            private bool _disableSweetFx;

            public bool DisableSweetFx {
                get { return _disableSweetFx; }
                set {
                    if (Equals(value, _disableSweetFx)) return;
                    _disableSweetFx = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _disableWatermark;

            public bool DisableWatermark {
                get { return _disableWatermark; }
                set {
                    if (Equals(value, _disableWatermark)) return;
                    _disableWatermark = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _cameraFov;

            public double CameraFov {
                get { return _cameraFov; }
                set {
                    if (Equals(value, _cameraFov)) return;
                    _cameraFov = Math.Min(Math.Max(value, 10.0), 150.0);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private ShowroomObject _selectedShowroom;

            public ShowroomObject SelectedShowroom {
                get { return _selectedShowroom; }
                set {
                    if (Equals(value, _selectedShowroom)) return;
                    _selectedShowroom = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private PpFilterObject _selectedFilter;

            public PpFilterObject SelectedFilter {
                get { return _selectedFilter; }
                set {
                    if (Equals(value, _selectedFilter)) return;
                    _selectedFilter = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public AcLoadedOnlyCollection<ShowroomObject> Showrooms => ShowroomsManager.Instance.LoadedOnlyCollection;

            public AcLoadedOnlyCollection<PpFilterObject> Filters => PpFiltersManager.Instance.LoadedOnlyCollection;

            public bool CanBeSaved => SelectedShowroom != null && SelectedFilter != null;

            public string UserPresetableKey => UserPresetableKeyValue;

            public string ExportToUserPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromUserPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            public bool Run() {
                if (SelectedCar == null || SelectedSkinId == null || SelectedShowroom == null || SelectedFilter == null) return false;
                RunAsync();
                return true;
            }

            public string ForceFilterAcId;

            public async void RunAsync() {
                try {
                    await Task.Run(() => Showroom.Start(new Showroom.ShowroomProperties {
                        AcRoot = AcRootDirectory.Instance.Value,
                        CarId = SelectedCar.Id,
                        CarSkinId = SelectedSkinId,
                        ShowroomId = SelectedShowroom.Id,
                        CameraFov = CameraFov,
                        DisableSweetFx = DisableSweetFx,
                        DisableWatermark = DisableSweetFx,
                        Filter = ForceFilterAcId ?? SelectedFilter.AcId,
                        UseBmp = false
                    }));
                } catch (Exception e) {
                    ShowMessage("Can’t start showroom:\n\n" + e, "Oops!", MessageBoxButton.OK);
                    Logging.Warning("cannot update previews: " + e);
                }
            }
        }

        public CarOpenInShowroomDialogViewModel ViewModel => (CarOpenInShowroomDialogViewModel) DataContext;

        public CarOpenInShowroomDialog(CarObject carObject, string selectedSkinId) {
            InitializeComponent();
            DataContext = new CarOpenInShowroomDialogViewModel(carObject, selectedSkinId);

            Buttons = new[] { GoButton, CloseButton };
        }

        private void CarOpenInShowroomDialog_OnClosing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;
            ViewModel.Run();
        }

        public static bool Run(CarObject carObject, string selectedSkinId, string filterAcId = null) {
            return new CarOpenInShowroomDialogViewModel(string.Empty, carObject, selectedSkinId) {
                ForceFilterAcId = filterAcId
            }.Run();
        }

        public static bool RunPreset(string presetFilename, CarObject carObject, string selectedSkinId) {
            return new CarOpenInShowroomDialogViewModel(File.ReadAllText(presetFilename), carObject, selectedSkinId).Run();
        }
    }
}
