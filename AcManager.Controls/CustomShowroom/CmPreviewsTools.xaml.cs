using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
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

namespace AcManager.Controls.CustomShowroom {
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

            this.OnActualUnload(() => {
                Model.Dispose();
            });
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

            public bool MagickNetEnabled => PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId);

            private CarObject _car;

            [CanBeNull]
            public CarObject Car {
                get { return _car; }
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                }
            }

            private CarSkinObject _skin;

            [CanBeNull]
            public CarSkinObject Skin {
                get { return _skin; }
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
                if (renderer == null) throw new ArgumentNullException(nameof(renderer));

                Renderer = renderer;
                Settings = new CmPreviewsSettings(renderer);

                renderer.PropertyChanged += Renderer_PropertyChanged;
                Renderer_CarNodeUpdated();
                renderer.Tick += Renderer_Tick;

                Car = carObject;
                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>("__CmPreviewsTools", () => new SaveableData(), o => {
                }, () => {
                    Reset(false);
                });
            }

            private void Reset(bool saveLater) {

                if (saveLater) {
                    SaveLater();
                }
            }

            private INotifyPropertyChanged _carNode;

            private void Renderer_CarNodeUpdated() {
                if (_carNode != null) {
                    _carNode.PropertyChanged -= CarNode_PropertyChanged;
                }

                _carNode = Renderer.CarNode;

                var carId = Renderer.CarNode?.CarId;
                Car = carId == null ? null : CarsManager.Instance.GetById(carId);
                Skin = Car?.GetSkinById(Renderer.CarNode?.CurrentSkin ?? "");

                if (_carNode != null) {
                    _carNode.PropertyChanged += CarNode_PropertyChanged;
                }
            }

            private void CarNode_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.CarNode.CurrentSkin):
                        Skin = Car?.GetSkinById(Renderer.CarNode?.CurrentSkin ?? "");
                        break;
                }
            }

            private void Renderer_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.MagickOverride):
                        SaveLater();
                        break;

                    case nameof(Renderer.CarNode):
                        Renderer_CarNodeUpdated();
                        break;
                }
            }

            private void Renderer_Tick(object sender, AcTools.Render.Base.TickEventArgs args) {}

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
                get { return _toUpdate; }
                set {
                    if (Equals(value, _toUpdate)) return;
                    _toUpdate = value;
                    OnPropertyChanged();
                    SingleSkin = value == null || value.Count != 1 || value[0].Skins?.Count != 1;
                }
            }

            private bool _singleSkin;

            public bool SingleSkin {
                get { return _singleSkin; }
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
                _errors = await UpdatePreview(new[] {
                    new ToUpdatePreview(car, skin)
                }, Settings.ToPreviewsOptions(), GetPresetName(), _previewsUpdater);
            }));

            private AsyncCommand _applyAllCommand;

            public AsyncCommand ApplyAllCommand => _applyAllCommand ?? (_applyAllCommand = new AsyncCommand(async () => {
                var car = Car;
                if (car == null) return;

                PrepareUpdater();
                _errors = await UpdatePreview(ToUpdate ?? new[] {
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

        [ItemCanBeNull]
        private static async Task<IReadOnlyList<UpdatePreviewError>> UpdatePreview(IReadOnlyList<ToUpdatePreview> entries, DarkPreviewsOptions options, string presetName = null,
                DarkPreviewsUpdater updater = null) {
            var localUpdater = updater == null;
            if (localUpdater) {
                updater = new DarkPreviewsUpdater(AcRootDirectory.Instance.RequireValue, options);
            } else {
                updater.SetOptions(options);
            }

            var errors = new List<UpdatePreviewError>();

            try {
                if (options.Showroom != null && ShowroomsManager.Instance.GetById(options.Showroom) == null) {
                    if (options.Showroom == "at_previews" && MissingShowroomHelper != null) {
                        await MissingShowroomHelper.OfferToInstall("Kunos Previews Showroom (AT Previews Special)", "at_previews",
                                "http://www.assettocorsa.net/assetto-corsa-v1-5-dev-diary-part-33/");
                        if (ShowroomsManager.Instance.GetById(options.Showroom) != null) goto Action;
                    }

                    throw new InformativeException("Can’t update preview", $"Showroom “{options.Showroom}” is missing");
                }

                Action:
                var checksum = options.GetChecksum();

                var finished = false;
                int j;

                using (var waiting = new WaitingDialog()) {
                    var cancellation = waiting.CancellationToken;

                    var singleMode = entries.Count == 1;
                    var verySingleMode = singleMode && entries[0].Skins?.Count == 1;
                    var recycled = 0;

                    if (!verySingleMode) {
                        waiting.SetImage(null);

                        if (SettingsHolder.CustomShowroom.PreviewsRecycleOld) {
                            waiting.SetMultiline(true);
                        }
                    }

                    var step = 1d / entries.Count;
                    var postfix = string.Empty;

                    var started = Stopwatch.StartNew();
                    var approximateSkinsPerCarCars = 1;
                    var approximateSkinsPerCarSkins = 10;

                    Action<int> updateApproximate = skinsPerCar => {
                        approximateSkinsPerCarCars++;
                        approximateSkinsPerCarSkins += skinsPerCar;
                    };

                    Func<int, int> leftSkins = currentEntry => {
                        var skinsPerCar = (double)approximateSkinsPerCarSkins / approximateSkinsPerCarCars;

                        var result = 0d;
                        for (var k = currentEntry + 1; k < entries.Count; k++) {
                            var entry = entries[k];
                            result += entry.Skins?.Count ?? skinsPerCar;
                        }

                        return result.RoundToInt();
                    };

                    var shotSkins = 0;
                    var recyclingWarning = false;
                    Func<int, CarObject, CarSkinObject, int?, IEnumerable<string>> getDetails = (currentIndex, car, skin, currentEntrySkinsLeft) => {
                        var left = leftSkins(currentIndex) + (currentEntrySkinsLeft ?? approximateSkinsPerCarSkins / approximateSkinsPerCarCars);

                        // ReSharper disable once AccessToModifiedClosure
                        var speed = shotSkins / started.Elapsed.TotalMinutes;
                        var remainingTime = speed < 0.0001 ? "Unknown" : $"About {TimeSpan.FromMinutes(left / speed).ToReadableTime()}";
                        var remainingItems = $"About {left} {PluralizingConverter.Pluralize(left, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}";

                        return new[] {
                            $"Car: {car?.DisplayName}",
                            $"Skin: {skin?.DisplayName ?? "?"}",
                            $"Speed: {speed:F2} {PluralizingConverter.Pluralize(10, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}/{"min"}",
                            $"Time remaining: {remainingTime}",
                            $"Items remaining: {remainingItems}",

                            // ReSharper disable once AccessToModifiedClosure
                            recyclingWarning ? "[i]Recycling seems to take too long? If so, it can always be disabled in Settings.[/i]" : null
                        }.NonNull();
                    };

                    for (j = 0; j < entries.Count; j++) {
                        if (cancellation.IsCancellationRequested) goto Cancel;

                        var entry = entries[j];
                        var progress = step * j;

                        var car = entry.Car;
                        var skins = entry.Skins;

                        if (skins == null) {
                            waiting.Report(new AsyncProgressEntry("Loading skins…" + postfix, verySingleMode ? 0d : progress));
                            waiting.SetDetails(getDetails(j, car, null, null));

                            await car.SkinsManager.EnsureLoadedAsync();
                            if (cancellation.IsCancellationRequested) goto Cancel;

                            skins = car.EnabledOnlySkins.ToList();
                            updateApproximate(skins.Count);
                        }

                        var halfstep = step * 0.5 / skins.Count;
                        for (var i = 0; i < skins.Count; i++) {
                            if (cancellation.IsCancellationRequested) goto Cancel;

                            var skin = skins[i];
                            waiting.SetDetails(getDetails(j, car, skin, skins.Count - i));

                            var subprogress = progress + step * (0.1 + 0.8 * i / skins.Count);
                            if (SettingsHolder.CustomShowroom.PreviewsRecycleOld && File.Exists(skin.PreviewImage)) {
                                if (++recycled > 5) {
                                    recyclingWarning = true;
                                }

                                waiting.Report(new AsyncProgressEntry($"Recycling current preview for {skin.DisplayName}…" + postfix, verySingleMode ? 0d : subprogress));
                                await Task.Run(() => FileUtils.Recycle(skin.PreviewImage));
                            }

                            waiting.Report(new AsyncProgressEntry($"Updating skin {skin.DisplayName}…" + postfix, verySingleMode ? 0d : subprogress + halfstep));

                            try {
                                await updater.ShotAsync(car.Id, skin.Id, skin.PreviewImage, car.AcdData, GetInformation(car, skin, presetName, checksum),
                                        () => {
                                            if (!verySingleMode) {
                                                ActionExtension.InvokeInMainThreadAsync(() => {
                                                    // ReSharper disable once AccessToModifiedClosure
                                                    if (!finished) {
                                                        // ReSharper disable once AccessToDisposedClosure
                                                        waiting.SetImage(skin.PreviewImage);
                                                    }
                                                });
                                            }
                                        });
                                shotSkins++;
                            } catch (Exception e) {
                                if (errors.All(x => x.ToUpdate != entry)) {
                                    errors.Add(new UpdatePreviewError(entry, e.Message, null));
                                }
                            }
                        }
                    }

                    waiting.Report(new AsyncProgressEntry("Saving…" + postfix, verySingleMode ? 0d : 0.999999d));
                    await updater.WaitForProcessing();

                    finished = true;
                }

                if (errors.Count > 0) {
                    NonfatalError.Notify("Can’t update previews:\n" + errors.Select(x => @"• " + x.Message.ToSentence()).JoinToString(";" + Environment.NewLine));
                }

                goto End;

                Cancel:
                for (; j < entries.Count; j++) {
                    errors.Add(new UpdatePreviewError(entries[j], ControlsStrings.Common_Cancelled, null));
                }

                End:
                return errors;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update preview", e);
                return null;
            } finally {
                if (localUpdater) {
                    updater.Dispose();
                }
            }
        }

        /// <summary>
        /// Last used settings will be used.
        /// </summary>
        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> UpdatePreview(IReadOnlyList<ToUpdatePreview> entries, string presetFilename = null) {
            return UpdatePreview(entries, CmPreviewsSettings.GetSavedOptions(presetFilename), GetPresetName());
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
