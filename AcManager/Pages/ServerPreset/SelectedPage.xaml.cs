using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.AcdFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace AcManager.Pages.ServerPreset {
    public partial class SelectedPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public static ServerPresetAssistState[] AssistStates { get; } = EnumExtension.GetValues<ServerPresetAssistState>();

        public static ServerPresetJumpStart[] JumpStarts { get; } = EnumExtension.GetValues<ServerPresetJumpStart>();

        public static ServerPresetRaceJoinType[] RaceJoinTypes { get; } = EnumExtension.GetValues<ServerPresetRaceJoinType>();

        private class ProgressCapacityConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return Math.Max((value.AsDouble() - 2d) / 10d, 1d);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ProgressCapacityConverter { get; } = new ProgressCapacityConverterInner();

        private class ClientsToBandwidthConverterInner : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                if (values.Length != 2) return null;
                var hz = values[0].AsDouble();
                var mc = values[1].AsDouble();
                return 384d * hz * mc * (mc - 1) / 1e6;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IMultiValueConverter ClientsToBandwidthConverter { get; } = new ClientsToBandwidthConverterInner();

        public class ViewModel : SelectedAcObjectViewModel<ServerPresetObject> {
            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get => _track;
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();

                    SelectedObject.TrackId = _track.MainTrackObject.Id;
                    SelectedObject.TrackLayoutId = _track.LayoutId;

                    MaximumCapacity = _track.SpecsPitboxesValue;
                }
            }

            private int _maximumCapacity;

            public int MaximumCapacity {
                get => _maximumCapacity;
                set {
                    if (Equals(value, _maximumCapacity)) return;
                    _maximumCapacity = value;
                    OnPropertyChanged();
                }
            }

            public BetterObservableCollection<CarObject> Cars { get; }

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                Track = SelectTrackDialog.Show(Track);
            }));

            public ViewModel([NotNull] ServerPresetObject acObject, TrackObjectBase track, CarObject[] cars) : base(acObject) {
                SelectedObject.PropertyChanged += OnAcObjectPropertyChanged;

                Track = track;
                Cars = new BetterObservableCollection<CarObject>(cars);
                Cars.CollectionChanged += OnCarsCollectionChanged;
            }

            private void OnCarsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                SelectedObject.CarIds = Cars.Select(x => x.Id).ToArray();
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= OnAcObjectPropertyChanged;
            }

            private void OnAcObjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(SelectedObject.TrackId):
                    case nameof(SelectedObject.TrackLayoutId):
                        Track = TracksManager.Instance.GetLayoutById(SelectedObject.TrackId, SelectedObject.TrackLayoutId);
                        break;

                    case nameof(SelectedObject.CarIds):
                        Cars.ReplaceEverythingBy(SelectedObject.CarIds.Select(x => CarsManager.Instance.GetById(x)));
                        break;
                }
            }

            [Localizable(false)]
            private static string GetPackedDataFixReadMe(string serverName, IEnumerable<string> notPacked) {
                return $@"Packed data for server {serverName}

    Cars:
{notPacked.Select(x => $"    - {x};").JoinToString("\n").ApartFromLast(";") + "."}

    Installation:
    - Find AC's content/cars directory;
    - Unpack there folders to it.

    Don't forget to make a backup if you already have data packed and it's
different.";
            }

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand = new AsyncCommand(async () => {
                var notPacked = Cars.Where(x => x?.AcdData?.IsPacked == false).Select(x => x.DisplayName).ToList();
                WaitingDialog waiting = null;
                try {
                    if (notPacked.Any() &&
                            ModernDialog.ShowMessage(string.Format(ToolsStrings.ServerPreset_UnpackedDataWarning, notPacked.JoinToReadableString()),
                                    ToolsStrings.ServerPreset_UnpackedDataWarning_Title, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        waiting = new WaitingDialog();

                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                var i = 0;
                                foreach (var car in Cars.Where(x => x.AcdData?.IsPacked == false)) {
                                    waiting.Report(new AsyncProgressEntry(car.DisplayName, i++, notPacked.Count));

                                    await Task.Delay(50, waiting.CancellationToken);
                                    if (waiting.CancellationToken.IsCancellationRequested) return;

                                    var dataDirectory = Path.Combine(car.Location, "data");
                                    var destination = Path.Combine(car.Location, "data.acd");
                                    Acd.FromDirectory(dataDirectory).Save(destination);

                                    writer.Write(Path.Combine(car.Id, "data.acd"), destination);
                                }

                                writer.WriteString(@"ReadMe.txt", GetPackedDataFixReadMe(SelectedObject.DisplayName, notPacked));
                            }

                            var temporary = FileUtils.EnsureUnique(FilesStorage.Instance.GetTemporaryFilename(
                                    FileUtils.EnsureFileNameIsValid($@"Fix for {SelectedObject.DisplayName}.zip")));
                            await FileUtils.WriteAllBytesAsync(temporary, memory.ToArray(), waiting.CancellationToken);
                            WindowsHelper.ViewFile(temporary);
                        }
                    } else {
                        waiting = new WaitingDialog();
                    }

                    await SelectedObject.RunServer(waiting, waiting.CancellationToken);
                } catch (TaskCanceledException) {
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t run server", e);
                } finally {
                    waiting?.Dispose();
                }
            }, () => SelectedObject.RunServerCommand.IsAbleToExecute).ListenOnWeak(SelectedObject.RunServerCommand));

            private AsyncCommand _restartCommand;

            public AsyncCommand RestartCommand => _restartCommand ?? (_restartCommand = new AsyncCommand(() => {
                SelectedObject.StopServer();
                GoCommand.ExecuteAsync().Forget();
                return Task.Delay(0);
            }, () => SelectedObject.RestartServerCommand.IsAbleToExecute).ListenOnWeak(SelectedObject.RestartServerCommand));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private ServerPresetObject _object;
        private TrackObjectBase _track;
        private CarObject[] _cars;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ServerPresetsManager.Instance.GetByIdAsync(_id);
            if (_object == null) return;

            _track = _object.TrackId == null ? null : await TracksManager.Instance.GetLayoutByIdAsync(_object.TrackId, _object.TrackLayoutId);
            _cars = (await _object.CarIds.Select(x => CarsManager.Instance.GetByIdAsync(x)).WhenAll(4)).ToArray();
        }

        void ILoadableContent.Load() {
            _object = ServerPresetsManager.Instance.GetById(_id);
            if (_object == null) return;

            _track = _object.TrackId == null ? null : TracksManager.Instance.GetLayoutById(_object.TrackId, _object.TrackLayoutId);
            _cars = _object.CarIds.Select(x => CarsManager.Instance.GetById(x)).ToArray();
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            SetModel();
            InitializeComponent();

            this.OnActualUnload(() => {
                _object?.UnsubscribeWeak(OnServerPropertyChanged);
            });
        }

        public bool ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = ServerPresetsManager.Instance.GetById(id);
            if (obj == null) return false;

            var track = obj.TrackId == null ? null : TracksManager.Instance.GetLayoutById(obj.TrackId, obj.TrackLayoutId);
            var cars = obj.CarIds.Select(x => CarsManager.Instance.GetById(x)).ToArray();

            _id = id;
            _object?.UnsubscribeWeak(OnServerPropertyChanged);
            _object = obj;
            _track = track;
            _cars = cars;

            SetModel();
            return true;
        }

        private void SetModel() {
            _model?.Unload();
            _object.SubscribeWeak(OnServerPropertyChanged);
            RunningLogLink.IsShown = _object.RunningLog != null;
            InitializeAcObjectPage(_model = new ViewModel(_object, _track, _cars));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.RestartCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
            });
        }

        private void OnServerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerPresetObject.RunningLog):
                    RunningLogLink.IsShown = _object.RunningLog != null;
                    break;
            }
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e) {
            IsRunningMessage.Visibility = Tabs.SelectedSource == TryFindResource(@"RunningLogUri") as Uri ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
