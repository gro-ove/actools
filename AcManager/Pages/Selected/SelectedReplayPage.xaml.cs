using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.LargeFilesSharing;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writer;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedReplayPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<ReplayObject> {
            public ViewModel([NotNull] ReplayObject acObject) : base(acObject) { }

            private WeatherObject _weather;

            public WeatherObject Weather {
                get { return _weather; }
                set {
                    if (Equals(value, _weather)) return;
                    _weather = value;
                    OnPropertyChanged();
                }
            }

            private CarObject _car;

            public CarObject Car {
                get { return _car; }
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                }
            }

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin {
                get { return _carSkin; }
                set {
                    if (Equals(value, _carSkin)) return;
                    _carSkin = value;
                    OnPropertyChanged();
                }
            }

            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get { return _track; }
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                }
            }

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
                }

                base.FilterExec(type);
            }

            /// <summary>
            /// Gets description for ReadMe.txt inside shared archive.
            /// </summary>
            /// <returns></returns>
            private string GetDescription() {
                return Car == null
                        ? (Track == null ? ToolsStrings.Common_AcReplay : $"{ToolsStrings.Common_AcReplay} ({Track.DisplayName})")
                        : (Track == null
                                ? $"{ToolsStrings.Common_AcReplay} ({Car.DisplayName})"
                                : $"{ToolsStrings.Common_AcReplay} ({Car.DisplayName}, {Track.DisplayName})");
            }

            private ICommandExt _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async () => {
                UploadResult result;

                try {
                    using (var waiting = new WaitingDialog(ControlsStrings.Common_Uploading)) {
                        waiting.Report(ControlsStrings.Common_Preparing);

                        var uploader = Uploaders.List.First();
                        await uploader.SignIn(waiting.CancellationToken);

                        waiting.Report(ControlsStrings.Common_Compressing);

                        byte[] data = null;
                        await Task.Run(() => {
                            using (var file = new FileStream(SelectedObject.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var memory = new MemoryStream()) {
                                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                    var readMe = string.Format(AppStrings.Replay_SharedReadMe, GetDescription(),
                                            SelectedObject.Id);
                                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(readMe))) {
                                        writer.Write(@"ReadMe.txt", stream);
                                    }

                                    writer.Write(SelectedObject.Id, file, SelectedObject.CreationTime);
                                }

                                data = memory.ToArray();
                                Logging.Write($"Compressed: {file.Length.ToReadableSize()} to {data.LongLength.ToReadableSize()}");
                            }
                        });

                        result = await uploader.Upload(SelectedObject.Name ?? AppStrings.Replay_DefaultUploadedName,
                                Path.GetFileNameWithoutExtension(SelectedObject.Location) + @".zip",
                                @"application/zip", GetDescription(), data, waiting,
                                waiting.CancellationToken);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Replay_CannotShare, ToolsStrings.Common_MakeSureInternetWorks, e);
                    return;
                }

                SharingHelper.Instance.AddToHistory(SharedEntryType.Replay, SelectedObject.Name, Car?.DisplayName, result);
                SharingUiHelper.ShowShared(SharedEntryType.Replay, result.Url);
            }));

            private ICommand _playCommand;

            public ICommand PlayCommand => _playCommand ?? (_playCommand = new AsyncCommand(async () => {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Name = SelectedObject.Id,
                    TrackId = SelectedObject.TrackId,
                    TrackConfiguration = SelectedObject.TrackConfiguration,
                    WeatherId = SelectedObject.WeatherId
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

            InitializeAcObjectPage(_model = new ViewModel(_object) {
                Track = _trackObject,
                Car = _carObject,
                CarSkin = _carSkinObject,
                Weather = _weather
            });
            InputBindings.AddRange(new[] {
                new InputBinding(_model.PlayCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control))
            });
            InitializeComponent();

            FancyBackgroundManager.Instance.ChangeBackground(_trackObject?.PreviewImage);
        }
    }
}
