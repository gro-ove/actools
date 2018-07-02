using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.LargeFilesSharing;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writers;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedReplayPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        /// <summary>
        /// Gets description for ReadMe.txt inside shared archive.
        /// </summary>
        /// <returns></returns>
        private static string GetDescription([CanBeNull] CarObject car, [CanBeNull] TrackObjectBase track) {
            return car == null
                    ? (track == null ? ToolsStrings.Common_AcReplay : $"{ToolsStrings.Common_AcReplay} ({track.Name})")
                    : (track == null
                            ? $"{ToolsStrings.Common_AcReplay} ({car.DisplayName})"
                            : $"{ToolsStrings.Common_AcReplay} ({car.DisplayName}, {track.Name})");
        }

        public static async Task ShareReplay(string name, string filename, [CanBeNull] CarObject car, [CanBeNull] TrackObjectBase track, bool forceUpload) {
            if (!forceUpload) {
                var existing = GetShared(filename);
                if (existing != null) {
                    SharingUiHelper.ShowShared(SharedEntryType.Replay, existing);
                    return;
                }
            }

            UploadResult result;

            try {
                using (var waiting = new WaitingDialog(ControlsStrings.Common_Uploading)) {
                    waiting.Report(ControlsStrings.Common_Preparing);

                    var uploader = LargeFileUploaderParams.ShareReplays.SelectedUploader;
                    await uploader.SignInAsync(waiting.CancellationToken);

                    waiting.Report(ControlsStrings.Common_Compressing);

                    using (var memory = new MemoryStream()) {
                        await Task.Run(() => {
                            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                    var readMe = string.Format(AppStrings.Replay_SharedReadMe, GetDescription(car, track),
                                            Path.GetFileName(filename));
                                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(readMe))) {
                                        writer.Write(@"ReadMe.txt", stream);
                                    }

                                    writer.Write(Path.GetFileName(filename), file, new FileInfo(filename).CreationTime);
                                }

                                Logging.Write($"Compressed: {file.Length.ToReadableSize()} to {memory.Position.ToReadableSize()}");
                            }
                        });

                        memory.Position = 0;
                        result = await uploader.UploadAsync(name ?? AppStrings.Replay_DefaultUploadedName,
                                Path.GetFileNameWithoutExtension(filename) + @".zip",
                                @"application/zip", GetDescription(car, track), memory, UploadAs.Replay, waiting,
                                waiting.CancellationToken);
                    }
                }
            } catch (Exception e) when (e.IsCancelled()) {
                return;
            } catch (WebException e) {
                NonfatalError.Notify(AppStrings.Replay_CannotShare, ToolsStrings.Common_MakeSureInternetWorks, e);
                return;
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Replay_CannotShare, e);
                return;
            }

            SharingHelper.Instance.AddToHistory(SharedEntryType.Replay, name, car?.DisplayName, result);
            SharingUiHelper.ShowShared(SharedEntryType.Replay, result.Url);
            SetShared(filename, result.Url);
        }

        [CanBeNull]
        public static string GetShared(string filename) {
            var name = Path.GetFileName(filename)?.ApartFromLast(ReplayObject.ReplayExtension, StringComparison.OrdinalIgnoreCase);
            var info = new FileInfo(filename);
            return name == null || !info.Exists ? null : CacheStorage.Get<string>($@"sharedReplay:{name.ToLowerInvariant()}:{info.Length}");
        }

        public static void SetShared(string filename, string url) {
            var name = Path.GetFileName(filename)?.ApartFromLast(ReplayObject.ReplayExtension, StringComparison.OrdinalIgnoreCase);
            var info = new FileInfo(filename);
            if (name == null || !info.Exists) { } else {
                CacheStorage.Set($@"sharedReplay:{name.ToLowerInvariant()}:{info.Length}", url);
            }
        }

        public class ViewModel : SelectedAcObjectViewModel<ReplayObject> {
            public ViewModel([NotNull] ReplayObject acObject) : base(acObject) {
                UpdateAlreadyShared();

                _timeKey = @".ReplayTime:" + acObject.Id;
                Time = acObject.AllowToOverrideTime
                        ? CacheStorage.Get(_timeKey, acObject.TimeFrom ?? FlexibleParser.ParseTime(@"09:00"))
                        : acObject.TimeFrom ?? FlexibleParser.ParseTime(@"09:00");

                if (acObject.AllowToOverrideTime) {
                    TimeSliderMapper = new DiapasonMapper(WeatherManager.Instance.GetById(acObject.WeatherId)?.GetTimeDiapason()
                            ?? Diapason.CreateTime(@"0:00-23:59")) {
                                ActualValue = Time
                            };
                }
            }

            public void UpdateAlreadyShared() {
                AlreadyShared = GetShared(SelectedObject.Location) != null;
            }

            private bool _alreadyShared;

            public bool AlreadyShared {
                get => _alreadyShared;
                private set => Apply(value, ref _alreadyShared);
            }

            private WeatherObject _weather;

            public WeatherObject Weather {
                get => _weather;
                set => Apply(value, ref _weather);
            }

            private CarObject _car;

            public CarObject Car {
                get => _car;
                set => Apply(value, ref _car);
            }

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin {
                get => _carSkin;
                set => Apply(value, ref _carSkin);
            }

            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get => _track;
                set => Apply(value, ref _track);
            }

            private DiapasonMapper _timeSliderMapper;

            [CanBeNull]
            public DiapasonMapper TimeSliderMapper {
                get => _timeSliderMapper;
                set {
                    var oldValue = _timeSliderMapper;
                    Apply(value, ref _timeSliderMapper, () => {
                        oldValue?.UnsubscribeWeak(OnTimeSliderMapperChanged);
                        value?.SubscribeWeak(OnTimeSliderMapperChanged);
                        Time = value?.GetClosest(Time) ?? Time;
                    });
                }
            }

            private readonly Busy _syncTime = new Busy();

            private void OnTimeSliderMapperChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (propertyChangedEventArgs.PropertyName == nameof(TimeSliderMapper.ActualValue)) {
                    _syncTime.Do(() => Time = TimeSliderMapper?.ActualValue ?? Time);
                }
            }

            private readonly string _timeKey;
            private int _time;

            public int Time {
                get => _time;
                set {
                    if (!Apply(TimeSliderMapper?.GetClosest(value) ?? value, ref _time)) return;
                    OnPropertyChanged(nameof(DisplayTime));

                    _syncTime.Do(() => {
                        if (TimeSliderMapper != null) {
                            TimeSliderMapper.ActualValue = Time;
                        }
                    });

                    CacheStorage.Set(_timeKey, value);
                }
            }

            public string DisplayTime {
                get => _time.ToDisplayTime();
                set {
                    if (!FlexibleParser.TryParseTime(value, out var time)) return;
                    Time = time;
                }
            }

            private DelegateCommand _changeCategoryCommand;

            public DelegateCommand ChangeCategoryCommand => _changeCategoryCommand ?? (_changeCategoryCommand = new DelegateCommand(() => {
                const string key = "__replayLastUsedCategory";
                var newCategory = Prompt.Show("Where to move selected replay?", "Change Replay’s Category",
                        ValuesStorage.Get<string>(key), "?", required: true, maxLength: 60,
                        suggestions: ReplaysManager.Instance.Enabled.Select(x => x.Category).ApartFrom(ReplayObject.AutosaveCategory));
                if (string.IsNullOrWhiteSpace(newCategory)) return;

                ValuesStorage.Set(key, newCategory);
                SelectedObject.EditableCategory = FileUtils.EnsureFileNameIsValid(newCategory);
            }));

            #region Presets
            public override void Unload() {
                base.Unload();
                _helper.Dispose();
            }

            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set => Apply(value, ref _quickDrivePresets);
            }

            private HierarchicalItemsView _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue),
                            p => { QuickDrive.RunAsync(Car, CarSkin?.Id, track: Track, presetFilename: p.VirtualFilename).Forget(); });
                }
            }
            #endregion

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(Car, CarSkin?.Id, track: Track)) {
                    DriveOptionsCommand.Execute();
                }
            }));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand
                => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => { QuickDrive.Show(Car, CarSkin?.Id, track: Track); }));

            private DelegateCommand _clearCategoryCommand;

            public DelegateCommand ClearCategoryCommand
                =>
                        _clearCategoryCommand
                                ?? (_clearCategoryCommand =
                                        new DelegateCommand(() => { SelectedObject.EditableCategory = null; }, () => SelectedObject.EditableCategory != null))
                                        .ListenOnWeak(SelectedObject, nameof(SelectedObject.EditableCategory));

            private AsyncCommand _keepReplayCommand;

            public AsyncCommand KeepReplayCommand => _keepReplayCommand ?? (_keepReplayCommand = new AsyncCommand(async () => {
                SelectedObject.EditableCategory = null;

                await Task.Yield();
                await SelectedObject.SaveCommand.ExecuteAsync();
            }, () => SelectedObject.EditableCategory == ReplayObject.AutosaveCategory && AcSettingsHolder.Replay.Autosave))
                    .ListenOnWeak(SelectedObject, nameof(SelectedObject.EditableCategory))
                    .ListenOnWeak(AcSettingsHolder.Replay, nameof(AcSettingsHolder.Replay.Autosave));

            [Localizable(false)]
            protected override void FilterExec(string type) {
                switch (type) {
                    case "size":
                        FilterRange("size", SelectedObject.Size.ToMegabytes(), 0.2, roundTo: 0.1);
                        return;

                    case "driver":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.DriverName) ? "driver-" : $"driver:{Filter.Encode(SelectedObject.DriverName)}");
                        return;

                    case "car":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.CarId) ? "carid-" : $"carid:{Filter.Encode(SelectedObject.CarId)}");
                        return;

                    case "skin":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.CarSkinId) ? "skinid-" : $"skinid:{Filter.Encode(SelectedObject.CarSkinId)}");
                        return;

                    case "track":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.TrackId) ? "trackid-" : $"trackid:{Filter.Encode(SelectedObject.TrackId)}");
                        return;

                    case "weather":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.WeatherId) ? "weatherid-" : $"weatherid:{Filter.Encode(SelectedObject.WeatherId)}");
                        return;

                    case "recordingquality":
                        NewFilterTab(SelectedObject.RecordingQuality == null
                                ? "recordingquality-" : $"recordingquality:{Filter.Encode(SelectedObject.RecordingQuality.Value.ToInvariantString())} Hz");
                        return;

                    case "version":
                        NewFilterTab($"version:{Filter.Encode(SelectedObject.Version.ToInvariantString())}");
                        return;

                    case "cars":
                        NewFilterTab(SelectedObject.CarsNumber == null ? "cars-" : $"cars:{Filter.Encode(SelectedObject.CarsNumber.Value.ToInvariantString())}");
                        return;

                    case "duration":
                        var duration = SelectedObject.Duration;
                        if (duration == null) {
                            NewFilterTab("time-");
                        } else {
                            FilterRange("time", duration.Value, TimeSpan.FromMinutes(5));
                        }
                        return;

                    case "time":
                        var time = SelectedObject.CustomTime ?? SelectedObject.TimeFrom;
                        if (time == null) {
                            NewFilterTab("time-");
                        } else {
                            FilterRange("time", TimeSpan.FromSeconds(time.Value), TimeSpan.FromMinutes(30));
                        }
                        return;
                }

                base.FilterExec(type);
            }

            private AsyncCommand<bool?> _shareCommand;

            public AsyncCommand<bool?> ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand<bool?>(async v => {
                await ShareReplay(SelectedObject.Name, SelectedObject.Location, Car, Track, v ?? false);
                UpdateAlreadyShared();
            }));

            private AsyncCommand _playCommand;

            public AsyncCommand PlayCommand => _playCommand ?? (_playCommand = new AsyncCommand(async () => {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Name = SelectedObject.Id,
                    CarId = SelectedObject.CarId,
                    TrackId = SelectedObject.TrackId,
                    TrackConfiguration = SelectedObject.TrackConfiguration,
                    WeatherId = SelectedObject.WeatherId,
                    SunAngle = Game.ConditionProperties.GetSunAngle(SelectedObject.CustomTime ?? Time)
                }));
            }, () => !SelectedObject.HasError(AcErrorType.Replay_TrackIsMissing)));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        public bool ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                return false;
            }

            var obj = ReplaysManager.Instance.GetById(id);
            if (obj == null || !obj.ParsedSuccessfully) {
                return false;
            }

            _object = obj;

            if (obj.TrackId != null) {
                var trackObject = TracksManager.Instance.GetById(obj.TrackId);
                if (obj.TrackConfiguration != null) {
                    _trackObject = trackObject?.GetLayoutByLayoutId(obj.TrackConfiguration) ?? trackObject;
                } else {
                    _trackObject = trackObject;
                }
            }

            if (obj.CarId != null) {
                _carObject = CarsManager.Instance.GetById(obj.CarId);
                _carSkinObject = _carObject == null ? null :
                        (obj.CarSkinId != null ? _carObject.SkinsManager.GetById(obj.CarSkinId) : null) ?? _carObject.SelectedSkin;
            }

            if (obj.WeatherId != null) {
                _weather = WeatherManager.Instance.GetById(obj.WeatherId);
            }

            SetModel();
            return true;
        }

        private ReplayObject _object;
        private TrackObjectBase _trackObject;
        private CarObject _carObject;
        private CarSkinObject _carSkinObject;
        private WeatherObject _weather;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ReplaysManager.Instance.GetByIdAsync(_id);

            if (_object?.TrackId != null) {
                var trackObject = await TracksManager.Instance.GetByIdAsync(_object.TrackId);
                if (_object.TrackConfiguration != null) {
                    _trackObject = trackObject?.GetLayoutByLayoutId(_object.TrackConfiguration) ?? trackObject;
                } else {
                    _trackObject = trackObject;
                }
            }

            if (_object?.CarId != null) {
                _carObject = await CarsManager.Instance.GetByIdAsync(_object.CarId);
                _carSkinObject = _carObject == null ? null :
                        (_object.CarSkinId != null ? await _carObject.SkinsManager.GetByIdAsync(_object.CarSkinId) : null) ?? _carObject.SelectedSkin;
            }

            if (_object?.WeatherId != null) {
                _weather = await WeatherManager.Instance.GetByIdAsync(_object.WeatherId);
            }
        }

        void ILoadableContent.Load() {
            _object = ReplaysManager.Instance.GetById(_id);

            if (_object?.TrackId != null) {
                var trackObject = TracksManager.Instance.GetById(_object.TrackId);
                if (_object.TrackConfiguration != null) {
                    _trackObject = trackObject?.GetLayoutByLayoutId(_object.TrackConfiguration) ?? trackObject;
                } else {
                    _trackObject = trackObject;
                }
            }

            if (_object?.CarId != null) {
                _carObject = CarsManager.Instance.GetById(_object.CarId);
                _carSkinObject = _carObject == null ? null :
                        (_object.CarSkinId != null ? _carObject.SkinsManager.GetById(_object.CarSkinId) : null) ?? _carObject.SelectedSkin;
            }

            if (_object?.WeatherId != null) {
                _weather = WeatherManager.Instance.GetById(_object.WeatherId);
            }
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();

            FancyBackgroundManager.Instance.ChangeBackground(_trackObject?.PreviewImage);
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object) {
                Track = _trackObject,
                Car = _carObject,
                CarSkin = _carSkinObject,
                Weather = _weather
            });

            InputBindings.AddRange(new[] {
                new InputBinding(_model.PlayCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)),
            });
        }

        private void OnDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void OnCarPreviewClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            e.Handled = true;
            new ImageViewer<CarSkinObject>(
                    _model.CarSkin,
                    CarBlock.ImageViewerImageCallback,
                    CarBlock.ImageViewerDetailsCallback) {
                        MaxImageWidth = CommonAcConsts.PreviewWidth
                    }.ShowDialog();
        }
    }
}