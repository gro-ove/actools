using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public interface IMissingShowroomHelper {
        Task OfferToInstall([Localizable(false)] string showroomName, [Localizable(false)] string showroomId, [Localizable(false)] string informationUrl);
    }

    public partial class CmPreviewsTools {
        public static IMissingShowroomHelper MissingShowroomHelper { get; set; }

        private readonly string _loadPreset;

        public CmPreviewsTools(DarkKn5ObjectRenderer renderer, CarObject car, string skinId, string presetFilename) {
            _loadPreset = presetFilename;

            DataContext = new ViewModel(renderer, car, skinId);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.PreviewSkinCommand, new KeyGesture(Key.PageUp)),
                new InputBinding(Model.NextSkinCommand, new KeyGesture(Key.PageDown)),
                new InputBinding(Model.OpenSkinDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control))
            });
            InitializeComponent();
            Buttons = new Button[0];

            this.OnActualUnload(() => Model.Dispose());
        }

        private DispatcherTimer _timer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(300),
                IsEnabled = true
            };
            _timer.Tick += Timer_Tick;

            var saveable = Model.Settings;
            if (_loadPreset == null) {
                if (saveable.HasSavedData || UserPresetsControl.CurrentUserPreset != null) {
                    saveable.Initialize(false);
                } else {
                    saveable.Initialize(true);
                    UserPresetsControl.CurrentUserPreset =
                            UserPresetsControl.SavedPresets.FirstOrDefault(x => x.ToString() == @"Kunos");
                }
            } else {
                saveable.Initialize(true);
                UserPresetsControl.CurrentUserPreset =
                        UserPresetsControl.SavedPresets.FirstOrDefault(x => x.Filename == _loadPreset);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void Timer_Tick(object sender, EventArgs e) {
            Model.OnTick();
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            [NotNull]
            public DarkKn5ObjectRenderer Renderer { get; }

            [NotNull]
            public CmPreviewsSettings Settings { get; }

            private CarObject _car;

            [CanBeNull]
            public CarObject Car {
                get => _car;
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                }
            }

            private CarSkinObject _skin;

            [CanBeNull]
            public CarSkinObject Skin {
                get => _skin;
                set {
                    if (Equals(value, _skin)) return;
                    _skin = value;
                    OnPropertyChanged();

                    Renderer.SelectSkin(value?.Id);
                }
            }

            private class SaveableData {}

            private ISaveHelper Saveable { get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            public ViewModel([NotNull] DarkKn5ObjectRenderer renderer, [NotNull] CarObject carObject, string skinId) {
                Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
                Settings = new CmPreviewsSettings(renderer);

                renderer.PropertyChanged += OnRendererPropertyChanged;
                OnCarNodeUpdated();
                renderer.Tick += OnRendererTick;

                Car = carObject;
                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>("__CmPreviewsTools", () => new SaveableData(), o => {}, () => {
                    Reset(false);
                });
            }

            private void Reset(bool saveLater) {

                if (saveLater) {
                    SaveLater();
                }
            }

            private INotifyPropertyChanged _carNode;

            private void OnCarNodeUpdated() {
                if (_carNode != null) {
                    _carNode.PropertyChanged -= OnCarNodePropertyChanged;
                }

                var carNode = Renderer.CarNode;
                _carNode = carNode;

                var carId = Renderer.CarNode?.CarId;
                Car = carId == null ? null : CarsManager.Instance.GetById(carId);
                Skin = Car?.GetSkinById(Renderer.CarNode?.CurrentSkin ?? "");

                if (_carNode != null && carNode != null) {
                    _carNode.PropertyChanged += OnCarNodePropertyChanged;
                    carNode.BlurredNodesActive = false;
                    carNode.SeatbeltOnActive = false;
                    carNode.CockpitLrActive = false;
                }
            }

            private void OnCarNodePropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.CarNode.CurrentSkin):
                        Skin = Car?.GetSkinById(Renderer.CarNode?.CurrentSkin ?? "");
                        break;
                }
            }

            private void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.MagickOverride):
                        SaveLater();
                        break;

                    case nameof(Renderer.CarNode):
                        OnCarNodeUpdated();
                        break;
                }
            }

            private void OnRendererTick(object sender, AcTools.Render.Base.TickEventArgs args) {}

            public void OnTick() {}

            #region Commands
            private ICommand _nextSkinCommand;

            public ICommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new DelegateCommand(() => {
                Renderer.SelectNextSkin();
            }));

            private ICommand _previewSkinCommand;

            public ICommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new DelegateCommand(() => {
                Renderer.SelectPreviousSkin();
            }));

            private CommandBase _openSkinDirectoryCommand;

            public ICommand OpenSkinDirectoryCommand => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new DelegateCommand(() => {
                Skin?.ViewInExplorer();
            }, () => Skin != null));
            #endregion

            #region Apply, test
            [CanBeNull]
            private IReadOnlyList<UpdatePreviewError> _errors;

            [CanBeNull]
            public IReadOnlyList<UpdatePreviewError> GetErrors() {
                return _errors;
            }

            private IReadOnlyList<ToUpdatePreview> _toUpdate;

            public IReadOnlyList<ToUpdatePreview> ToUpdate {
                get => _toUpdate;
                set {
                    if (Equals(value, _toUpdate)) return;
                    _toUpdate = value;
                    OnPropertyChanged();
                    SingleSkin = value == null || value.Count != 1 || value[0].Skins?.Count != 1;
                }
            }

            private bool _singleSkin;

            public bool SingleSkin {
                get => _singleSkin;
                set {
                    if (Equals(value, _singleSkin)) return;
                    _singleSkin = value;
                    OnPropertyChanged();
                }
            }

            private DarkPreviewsUpdater _previewsUpdater;

            private void PrepareUpdater() {
                var options = Settings.ToPreviewsOptions();
                if (_previewsUpdater == null) {
                    _previewsUpdater = new DarkPreviewsUpdater(AcRootDirectory.Instance.RequireValue, options);
                } else {
                    _previewsUpdater.SetOptions(options);
                }
            }

            private AsyncCommand _testCommand;

            public AsyncCommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand(async () => {
                try {
                    var car = Car;
                    var skin = Skin;
                    if (car == null || skin == null) throw new Exception("Car or skin are not defined");

                    PrepareUpdater();

                    var temporary = FilesStorage.Instance.GetTemporaryFilename("Preview test.jpg");
                    var presetName = GetPresetName();
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Generating image…"));
                        await _previewsUpdater.ShotAsync(car.Id, skin.Id, temporary, car.AcdData,
                                GetInformation(car, skin, presetName, Settings.ToPreviewsOptions().GetChecksum()));

                        waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Saving…"));
                        await _previewsUpdater.WaitForProcessing();
                    }

                    if (new ImageViewer(new[] {
                        temporary,
                        skin.PreviewImage
                    }, 0, Settings.Width, Settings.Height).ShowDialogInSelectMode() != null) {
                        if (File.Exists(skin.PreviewImage)) {
                            FileUtils.Recycle(skin.PreviewImage);
                        }

                        File.Move(temporary, skin.PreviewImage);
                    }

                    _errors = new UpdatePreviewError[0];
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                    _errors = null;
                }
            }));

            private AsyncCommand _applyCommand;

            public AsyncCommand ApplyCommand => _applyCommand ?? (_applyCommand = new AsyncCommand(async () => {
                var car = Car;
                var skin = Skin;
                if (car == null || skin == null) throw new Exception("Car or skin are not defined");

                PrepareUpdater();
                _errors = await UpdatePreviewAsync(new[] {
                    new ToUpdatePreview(car, skin)
                }, Settings.ToPreviewsOptions(), GetPresetName(), _previewsUpdater);
            }));

            private AsyncCommand _applyAllCommand;

            public AsyncCommand ApplyAllCommand => _applyAllCommand ?? (_applyAllCommand = new AsyncCommand(async () => {
                var car = Car;
                if (car == null) return;

                PrepareUpdater();
                _errors = await UpdatePreviewAsync(ToUpdate ?? new[] {
                    new ToUpdatePreview(car)
                }, Settings.ToPreviewsOptions(), GetPresetName(), _previewsUpdater);
            }));
            #endregion

            public void Dispose() {
                DisposeHelper.Dispose(ref _previewsUpdater);
            }
        }

        private static string GetPresetName() {
            return Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(CmPreviewsSettings.DefaultPresetableKeyValue));
        }

        private static ImageUtils.ImageInformation GetInformation(CarObject car, CarSkinObject skin, string presetName, string checksum) {
            var result = new ImageUtils.ImageInformation {
                Software = $"ContentManager {BuildInformation.AppVersion}",
                Comment = presetName == null ? $"Settings checksum: {checksum}" : $"Used preset: {presetName} (checksum: {checksum})"
            };

            if (SettingsHolder.CustomShowroom.DetailedExifForPreviews) {
                result.Subject = $"{car.DisplayName}";
                result.Title = $"{car.DisplayName} ({skin.DisplayName})";
                result.Tags = car.Tags.ToArray();
                result.Author = skin.Author ?? car.Author;
            }

            return result;
        }

        private class Updater {
            private readonly IReadOnlyList<ToUpdatePreview> _entries;
            private readonly DarkPreviewsOptions _options;
            private readonly string _presetName;
            private readonly DarkPreviewsUpdater _updater;
            private readonly bool _localUpdater;
            private readonly List<UpdatePreviewError> _errors = new List<UpdatePreviewError>();
            private DispatcherTimer _dispatcherTimer;

            public Updater(IReadOnlyList<ToUpdatePreview> entries, DarkPreviewsOptions options, string presetName, DarkPreviewsUpdater updater) {
                _entries = entries;
                _options = options;
                _presetName = presetName;

                if (updater == null) {
                    _localUpdater = true;
                    _updater = new DarkPreviewsUpdater(AcRootDirectory.Instance.RequireValue, options);
                } else {
                    _updater = updater;
                    _updater.SetOptions(options);
                }
            }

            public async Task<IReadOnlyList<UpdatePreviewError>> Run() {
                try {
                    if (_options.Showroom != null && ShowroomsManager.Instance.GetById(_options.Showroom) == null) {
                        if (_options.Showroom == "at_previews" && MissingShowroomHelper != null) {
                            await MissingShowroomHelper.OfferToInstall("Kunos Previews Showroom (AT Previews Special)", "at_previews",
                                    "http://www.assettocorsa.net/assetto-corsa-v1-5-dev-diary-part-33/");
                            if (ShowroomsManager.Instance.GetById(_options.Showroom) != null) {
                                return await RunReady();
                            }
                        }

                        throw new InformativeException("Can’t update preview", $"Showroom “{_options.Showroom}” is missing");
                    }

                    return await RunReady();
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                    return null;
                } finally {
                    _waiting?.Dispose();
                    _dispatcherTimer?.Stop();
                    if (_localUpdater) {
                        _updater.Dispose();
                    }
                }
            }

            private WaitingDialog _waiting;
            private int _approximateSkinsPerCarCars = 1;
            private int _approximateSkinsPerCarSkins = 10;
            private Stopwatch _started;
            private int _shotSkins;
            private bool _recyclingWarning;
            private bool _finished;
            private bool _verySingleMode;
            private string _checksum;

            private int _i, _j;
            private CarObject _currentCar;
            private IReadOnlyList<CarSkinObject> _currentSkins;
            private CarSkinObject _currentSkin;

            private async Task<IReadOnlyList<UpdatePreviewError>> RunReady() {
                _checksum = _options.GetChecksum();

                _finished = false;
                _i = _j = 0;

                _waiting = new WaitingDialog { CancellationText = "Stop" };

                var singleMode = _entries.Count == 1;
                _verySingleMode = singleMode && _entries[0].Skins?.Count == 1;
                var recycled = 0;

                if (!_verySingleMode) {
                    _waiting.SetImage(null);

                    if (SettingsHolder.CustomShowroom.PreviewsRecycleOld) {
                        _waiting.SetMultiline(true);
                    }
                }

                var step = 1d / _entries.Count;
                var postfix = string.Empty;

                _started = Stopwatch.StartNew();

                _dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.Background, TimerCallback,
                        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
                _dispatcherTimer.Start();

                for (_j = 0; _j < _entries.Count; _j++) {
                    if (Cancel()) return _errors;

                    var entry = _entries[_j];
                    var progress = step * _j;

                    _currentCar = entry.Car;
                    _currentSkins = entry.Skins;

                    if (_currentSkins == null) {
                        _waiting.Report(new AsyncProgressEntry("Loading skins…" + postfix, _verySingleMode ? 0d : progress));
                        _waiting.SetDetails(GetDetails(_j, _currentCar, null, null));

                        await _currentCar.SkinsManager.EnsureLoadedAsync();
                        if (Cancel()) return _errors;

                        _currentSkins = _currentCar.EnabledOnlySkins.ToList();
                        UpdateApproximate(_currentSkins.Count);
                    }

                    var halfstep = step * 0.5 / _currentSkins.Count;
                    for (_i = 0; _i < _currentSkins.Count; _i++) {
                        if (Cancel()) return _errors;

                        _currentSkin = _currentSkins[_i];
                        _waiting.SetDetails(GetDetails(_j, _currentCar, _currentSkin, _currentSkins.Count - _i));

                        var subprogress = progress + step * (0.1 + 0.8 * _i / _currentSkins.Count);
                        if (SettingsHolder.CustomShowroom.PreviewsRecycleOld && File.Exists(_currentSkin.PreviewImage)) {
                            if (++recycled > 5) {
                                _recyclingWarning = true;
                            }

                            _waiting.Report(new AsyncProgressEntry($"Recycling current preview for {_currentSkin.DisplayName}…" + postfix,
                                    _verySingleMode ? 0d : subprogress));
                            await Task.Run(() => FileUtils.Recycle(_currentSkin.PreviewImage));
                        }

                        _waiting.Report(new AsyncProgressEntry($"Updating skin {_currentSkin.DisplayName}…" + postfix, _verySingleMode ? 0d : subprogress + halfstep));

                        try {
                            await _updater.ShotAsync(_currentCar.Id, _currentSkin.Id, _currentSkin.PreviewImage, _currentCar.AcdData,
                                    GetInformation(_currentCar, _currentSkin, _presetName, _checksum), PreviewReadyCallback);
                            _shotSkins++;
                        } catch (Exception e) {
                            if (_errors.All(x => x.ToUpdate != entry)) {
                                Logging.Warning(e);
                                _errors.Add(new UpdatePreviewError(entry, e.Message, null));
                            }
                        }
                    }
                }

                _dispatcherTimer?.Stop();
                _waiting.Report(new AsyncProgressEntry("Saving…" + postfix, _verySingleMode ? 0d : 0.999999d));
                await _updater.WaitForProcessing();

                _finished = true;

                if (_errors.Count > 0) {
                    NonfatalError.Notify("Can’t update previews:\n" + _errors.Select(x => @"• " + x.Message.ToSentence()).JoinToString(";" + Environment.NewLine));
                }

                return _errors;
            }

            private void PreviewReadyCallback() {
                if (!_verySingleMode) {
                    ActionExtension.InvokeInMainThreadAsync(UpdatePreviewImage);
                }
            }

            private void UpdatePreviewImage() {
                if (!_finished) {
                    _waiting.SetImage(_currentSkin.PreviewImage);
                }
            }

            private bool Cancel() {
                if (!_waiting.CancellationToken.IsCancellationRequested) return false;
                for (; _j < _entries.Count; _j++) {
                    _errors.Add(new UpdatePreviewError(_entries[_j], ControlsStrings.Common_Cancelled, null));
                }
                return true;
            }

            private void TimerCallback(object sender, EventArgs args) {
                if (_currentCar == null || _currentSkins == null || _currentSkin == null) return;
                _waiting.SetDetails(GetDetails(_j, _currentCar, _currentSkin, _currentSkins.Count - _i));
            }

            private void UpdateApproximate(int skinsPerCar) {
                _approximateSkinsPerCarCars++;
                _approximateSkinsPerCarSkins += skinsPerCar;
            }

            private int LeftSkins(int currentEntry) {
                var skinsPerCar = (double)_approximateSkinsPerCarSkins / _approximateSkinsPerCarCars;

                var result = 0d;
                for (var k = currentEntry + 1; k < _entries.Count; k++) {
                    var entry = _entries[k];
                    result += entry.Skins?.Count ?? skinsPerCar;
                }

                return result.RoundToInt();
            }

            private IEnumerable<string> GetDetails(int currentIndex, CarObject car, CarSkinObject skin, int? currentEntrySkinsLeft) {
                var left = LeftSkins(currentIndex) + (currentEntrySkinsLeft ?? _approximateSkinsPerCarSkins / _approximateSkinsPerCarCars);

                var speed = _shotSkins / _started.Elapsed.TotalMinutes;
                var remainingTime = speed < 0.0001 ? "Unknown" : $"About {TimeSpan.FromMinutes(left / speed).ToReadableTime()}";
                var remainingItems = $"About {left} {PluralizingConverter.Pluralize(left, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}";

                return new[] {
                    $"Car: {car?.DisplayName}", $"Skin: {skin?.DisplayName ?? "?"}", $"Speed: {speed:F2} {PluralizingConverter.Pluralize(10, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}/{"min"}", $"Time remaining: {remainingTime}", $"Items remaining: {remainingItems}",
                    _recyclingWarning ? "[i]Recycling seems to take too long? If so, it can always be disabled in Settings.[/i]" : null
                }.NonNull();
            }
        }

        [ItemCanBeNull]
        private static Task<IReadOnlyList<UpdatePreviewError>> UpdatePreviewAsync(IReadOnlyList<ToUpdatePreview> entries, DarkPreviewsOptions options,
                string presetName = null, DarkPreviewsUpdater updater = null) {
            return new Updater(entries, options, presetName, updater).Run();
        }

        /// <summary>
        /// Last used settings will be used.
        /// </summary>
        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> UpdatePreviewAsync(IReadOnlyList<ToUpdatePreview> entries, string presetFilename = null) {
            return UpdatePreviewAsync(entries, CmPreviewsSettings.GetSavedOptions(presetFilename), GetPresetName());
        }

        /// <summary>
        /// Get checksum of specified preset.
        /// </summary>
        public static string GetChecksum(string presetFilename = null) {
            return CmPreviewsSettings.GetSavedOptions(presetFilename).GetChecksum();
        }
    }

    public class ToUpdatePreview {
        [NotNull]
        public CarObject Car { get; }

        [CanBeNull]
        public IReadOnlyList<CarSkinObject> Skins { get; }

        /// <summary>
        /// Update skins in provided list.
        /// </summary>
        public ToUpdatePreview([NotNull] CarObject car, [CanBeNull] IReadOnlyList<CarSkinObject> skins) {
            Car = car;
            Skins = skins;
        }

        /// <summary>
        /// Update skins in provided list.
        /// </summary>
        public ToUpdatePreview([NotNull] CarObject car, [CanBeNull] IReadOnlyList<string> skinIds) {
            Car = car;
            Skins = skinIds?.Select(car.GetSkinById).NonNull().ToList();
        }

        /// <summary>
        /// Update only one skin.
        /// </summary>
        public ToUpdatePreview([NotNull] CarObject car, [CanBeNull] CarSkinObject skin) {
            Car = car;
            Skins = new [] { skin };
        }

        /// <summary>
        /// Update all enabled skins.
        /// </summary>
        public ToUpdatePreview([NotNull] CarObject car) {
            Car = car;
            Skins = null;
        }
    }

    public enum UpdatePreviewMode {
        Options, Start, StartManual
    }

    public class UpdatePreviewError {
        public UpdatePreviewError([NotNull] ToUpdatePreview toUpdate, [NotNull] string message, [CanBeNull] WhatsGoingOn whatsGoingOn) {
            ToUpdate = toUpdate;
            Message = message;
            WhatsGoingOn = whatsGoingOn;
        }

        [NotNull]
        public ToUpdatePreview ToUpdate { get; }

        [NotNull]
        public string Message { get; }

        [CanBeNull]
        public WhatsGoingOn WhatsGoingOn { get; }
    }
}
