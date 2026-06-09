using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Controls.Helpers;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.Dialogs {
    public class PresetSelection : IUserPresetable {
        private readonly IUserPresetable _parent;

        public PresetSelection(IUserPresetable parent, string key) {
            _parent = parent;
            PresetableKey = key;
        }

        public bool CanBeSaved => false;

        public PresetsCategory PresetableCategory => _parent.PresetableCategory;

        public string PresetableKey { get; }

        public string ExportToPresetData() {
            return null;
        }

        public event EventHandler Changed {
            add { }
            remove { }
        }

        public void ImportFromPresetData(string data) {
            Logging.Write(data);
        }
    }

    public partial class CarOpenInShowroomDialog {
        public const string PresetableKeyValue = "Showroom";

        public class ViewModel : NotifyPropertyChanged, IUserPresetable, IDisposable {
            private class SaveableData {
                public string ShowroomId, FilterId, VideoPresetFilename;
                public double CameraFov;
                public bool DisableSweetFx, DisableWatermark, UseCspShowroom;
            }

            private void SaveLater() {
                if (_saveable.SaveLater()) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }

            private readonly ISaveHelper _saveable;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public ViewModel(string serializedPreset, CarObject carObject, string selectedSkinId) {
                ShowroomsManager.Instance.EnsureLoaded();
                PpFiltersManager.Instance.EnsureLoaded();

                SelectedCar = carObject;
                SelectedSkinId = selectedSkinId ?? SelectedCar.SelectedSkin?.Id;

                VideoPresets = _helper.CreateGroup(AcSettingsHolder.VideoPresetsCategory, "", "Default");

                _saveable = new SaveHelper<SaveableData>("__CarOpenInShowroom", () => new SaveableData {
                    ShowroomId = SelectedShowroom?.Id,
                    FilterId = SelectedFilter?.Id,
                    CameraFov = CameraFov,
                    DisableSweetFx = DisableSweetFx,
                    DisableWatermark = DisableWatermark,
                    VideoPresetFilename = VideoPresetFilename,
                    UseCspShowroom = UseCspShowroom,
                }, o => {
                    if (o.ShowroomId != null) SelectedShowroom = ShowroomsManager.Instance.GetById(o.ShowroomId) ?? SelectedShowroom;
                    if (o.FilterId != null) SelectedFilter = PpFiltersManager.Instance.GetById(o.FilterId) ?? SelectedFilter;

                    CameraFov = o.CameraFov;
                    DisableWatermark = o.DisableWatermark;
                    DisableSweetFx = o.DisableSweetFx;
                    VideoPresetFilename = o.VideoPresetFilename;
                    UseCspShowroom = o.UseCspShowroom;
                }, () => {
                    SelectedShowroom = ShowroomsManager.Instance.GetDefault();
                    SelectedFilter = PpFiltersManager.Instance.GetDefault();
                    CameraFov = 30;
                    DisableWatermark = false;
                    DisableSweetFx = false;
                    VideoPresetFilename = null;
                    UseCspShowroom = false;
                });

                if (string.IsNullOrEmpty(serializedPreset)) {
                    _saveable.Initialize();
                } else {
                    _saveable.Reset();
                    _saveable.FromSerializedString(serializedPreset);
                }
            }

            public ViewModel(CarObject carObject, string selectedSkinId) : this(null, carObject, selectedSkinId) { }

            public CarObject SelectedCar { get; set; }

            [CanBeNull]
            public string SelectedSkinId { get; set; }

            private bool _disableSweetFx;

            public bool DisableSweetFx {
                get => _disableSweetFx;
                set {
                    if (Equals(value, _disableSweetFx)) return;
                    _disableSweetFx = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _disableWatermark;

            public bool DisableWatermark {
                get => _disableWatermark;
                set {
                    if (Equals(value, _disableWatermark)) return;
                    _disableWatermark = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _cameraFov;

            public double CameraFov {
                get => _cameraFov;
                set {
                    if (Equals(value, _cameraFov)) return;
                    _cameraFov = Math.Min(Math.Max(value, 10.0), 150.0);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private ShowroomObject _selectedShowroom;

            public ShowroomObject SelectedShowroom {
                get => _selectedShowroom;
                set {
                    if (Equals(value, _selectedShowroom)) return;
                    _selectedShowroom = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private PpFilterObject _selectedFilter;

            public PpFilterObject SelectedFilter {
                get => _selectedFilter;
                set {
                    if (Equals(value, _selectedFilter)) return;
                    _selectedFilter = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private HierarchicalGroup _videoPresets;

            public HierarchicalGroup VideoPresets {
                get => _videoPresets;
                set => Apply(value, ref _videoPresets);
            }

            private string _videoPresetFilename;

            [CanBeNull]
            public string VideoPresetFilename {
                get => _videoPresetFilename;
                set {
                    if (Equals(value, _videoPresetFilename)) return;
                    _videoPresetFilename = value;
                    OnPropertyChanged();
                    SaveLater();
                    DisplayVideoPreset = Path.GetFileNameWithoutExtension(value);
                }
            }

            private string _displayVideoPreset;

            [CanBeNull]
            public string DisplayVideoPreset {
                get => _displayVideoPreset;
                set => Apply(value, ref _displayVideoPreset);
            }

            public object SelectedVideoPreset {
                get => null;
                set {
                    if (value == null) {
                        VideoPresetFilename = null;
                    } else if (value is ISavedPresetEntry entry) {
                        VideoPresetFilename = entry.VirtualFilename;
                    }
                }
            }

            private bool _useCspShowroom;

            public bool UseCspShowroom {
                get => _useCspShowroom;
                set => Apply(value, ref _useCspShowroom, SaveLater);
            }

            public AcEnabledOnlyCollection<ShowroomObject> Showrooms => ShowroomsManager.Instance.Enabled;

            public AcEnabledOnlyCollection<PpFilterObject> Filters => PpFiltersManager.Instance.Enabled;

            public bool CanBeSaved => SelectedShowroom != null && SelectedFilter != null;
            string IUserPresetable.PresetableKey => PresetableKeyValue;
            PresetsCategory IUserPresetable.PresetableCategory => new PresetsCategory(PresetableKeyValue);

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            public bool Run() {
                if (SelectedCar == null || SelectedSkinId == null || SelectedShowroom == null || SelectedFilter == null) return false;
                RunAsync();
                return true;
            }

            public string ForceFilterAcId;

            public async void RunAsync() {
                string restoreVideoSettings = null;

                try {
                    if (VideoPresetFilename != null && File.Exists(VideoPresetFilename)) {
                        restoreVideoSettings = AcSettingsHolder.VideoPresets.ExportToPresetData();
                        AcSettingsHolder.VideoPresets.ImportFromPresetData(File.ReadAllText(VideoPresetFilename));
                    }

                    if (UseCspShowroom && PatchHelper.IsFeatureSupported(PatchHelper.FeatureHasShowroomMode)) {
                        await Game.StartAsync(GameWrapper.CreateStarter(), new Game.StartProperties {
                            BasicProperties = new Game.BasicProperties {
                                CarId = SelectedCar.Id,
                                CarSkinId = SelectedSkinId,
                                TrackId = $@"../showroom/{SelectedShowroom.Id}"
                            },
                            ConditionProperties = new Game.ConditionProperties {
                                AmbientTemperature = 26d,
                                RoadTemperature = 32d,
                                CloudSpeed = 1d,
                                SunAngle = 0,
                                TimeMultipler = 1d,
                                WeatherName = WeatherManager.Instance.GetDefault()?.Id,
                                WindDirectionDeg = 0d,
                                WindSpeedMin = 0d,
                                WindSpeedMax = 0d
                            },
                            ModeProperties = new Game.PracticeProperties {
                                SessionName = "Showroom",
                                Penalties = false
                            },
                        }, null, CancellationToken.None);
                    } else {
                        await Task.Run(() => Showroom.Start(new Showroom.ShowroomProperties {
                            AcRoot = AcRootDirectory.Instance.Value,
                            CarId = SelectedCar.Id,
                            CarSkinId = SelectedSkinId,
                            ShowroomId = SelectedShowroom.Id,
                            CameraFov = CameraFov,
                            DisableSweetFx = DisableSweetFx,
                            DisableWatermark = DisableWatermark,
                            Filter = ForceFilterAcId ?? SelectedFilter.AcId,
                            UseBmp = false
                        }));
                    }

                    var whatsGoingOn = AcLogHelper.TryToDetermineWhatsGoingOn();
                    if (whatsGoingOn != null) {
                        NonfatalError.Notify(whatsGoingOn.GetDescription(), solutions: new[] {
                            whatsGoingOn.Solution
                        });
                    }
                } catch (IOException e) {
                    NonfatalError.Notify(ControlsStrings.Showroom_CannotStart, e);
                } finally {
                    if (restoreVideoSettings != null) {
                        AcSettingsHolder.VideoPresets.ImportFromPresetData(restoreVideoSettings);
                    }
                }
            }

            public void Dispose() {
                _helper.Dispose();
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public CarOpenInShowroomDialog(CarObject carObject, string selectedSkinId) {
            InitializeComponent();
            DataContext = new ViewModel(carObject, selectedSkinId);
            Buttons = new[] { GoButton, CloseButton };
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;
            Model.Run();
            Model.Dispose();
        }

        public static bool Run(CarObject carObject, string selectedSkinId, string filterAcId = null) {
            return new ViewModel(string.Empty, carObject, selectedSkinId) {
                ForceFilterAcId = filterAcId
            }.Run();
        }

        public static bool RunPreset(string presetFilename, CarObject carObject, string selectedSkinId) {
            return new ViewModel(File.ReadAllText(presetFilename), carObject, selectedSkinId).Run();
        }
    }
}