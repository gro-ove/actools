using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.LargeFilesSharing;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writer;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public partial class SelectedReplayPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<ReplayObject> {
            public ViewModel([NotNull] ReplayObject acObject) : base(acObject) {
                DisplayParsed = acObject.ParsedSuccessfully ? "Yes" : "No";
            }

            public string DisplayParsed { get; }

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

            private TrackBaseObject _track;

            public TrackBaseObject Track {
                get { return _track; }
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                }
            }

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

            private string GetDescription() {
                return Car == null
                        ? (Track == null ? "AC replay" : $"AC replay (on {Track.DisplayName})")
                        : (Track == null ? $"AC replay ({Car.DisplayName})" : $"AC replay ({Car.DisplayName} on {Track.DisplayName})");
            }

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async o => {
                UploadResult result;

                try {
                    using (var waiting = new WaitingDialog("Uploading…")) {
                        waiting.Report("Preparing…");

                        var uploader = Uploaders.List.First();
                        await uploader.SignIn(waiting.CancellationToken);

                        waiting.Report("Compressing…");

                        byte[] data = null;
                        await Task.Run(() => {
                            using (var file = new FileStream(SelectedObject.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var memory = new MemoryStream()) {
                                using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                    var readMe = $"{GetDescription()}\n\nTo play, unpack “{SelectedObject.Id}” to “…\\Documents\\Assetto Corsa\\replay”.";
                                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(readMe))) {
                                        writer.Write("ReadMe.txt", stream);
                                    }

                                    writer.Write(SelectedObject.Id, file, SelectedObject.CreationTime);
                                }

                                data = memory.ToArray();
                                Logging.Write($"Compressed: {file.Length.ToReadableSize()} to {data.LongLength.ToReadableSize()}");
                            }
                        });

                        result = await uploader.Upload(SelectedObject.Name ?? "AC Replay", Path.GetFileNameWithoutExtension(SelectedObject.Location) + ".zip",
                                "application/zip", GetDescription(), data, waiting,
                                waiting.CancellationToken);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share replay", "Make sure Internet-connection works.", e);
                    return;
                }

                SharingHelper.Instance.AddToHistory(SharedEntryType.Replay, SelectedObject.Name, Car?.DisplayName, result);
                SharingUiHelper.ShowShared(SharedEntryType.Replay, result.Url);
            }));

            private ICommand _playCommand;

            public ICommand PlayCommand => _playCommand ?? (_playCommand = new AsyncCommand(async o => {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Name = SelectedObject.Id,
                    TrackId = SelectedObject.TrackId,
                    TrackConfiguration = SelectedObject.TrackConfiguration
                }));
            }, o => !SelectedObject.HasError(AcErrorType.Replay_TrackIsMissing)));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private ReplayObject _object;
        private TrackBaseObject _trackObject;
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
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

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
        }
    }
}
