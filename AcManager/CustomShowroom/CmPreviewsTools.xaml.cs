using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using AcTools;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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
            _timer.Tick += OnTimerTick;

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
                        UserPresetsControl.SavedPresets.FirstOrDefault(x => x.VirtualFilename == _loadPreset);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void OnTimerTick(object sender, EventArgs e) {
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
                set => Apply(value, ref _car);
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

            private class SaveableData { }

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
                Car.SkinsManager.EnsureLoadedAsync().Ignore();

                Saveable = new SaveHelper<SaveableData>("__CmPreviewsTools", () => new SaveableData(), o => { }, 
                        () => Reset(false));
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

            private void OnRendererTick(object sender, AcTools.Render.Base.TickEventArgs args) { }

            public void OnTick() { }

            #region Commands
            private ICommand _nextSkinCommand;

            public ICommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new DelegateCommand(() => { Renderer.SelectNextSkin(); }));

            private ICommand _previewSkinCommand;

            public ICommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new DelegateCommand(() => { Renderer.SelectPreviousSkin(); }));

            private CommandBase _openSkinDirectoryCommand;

            public ICommand OpenSkinDirectoryCommand
                => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new DelegateCommand(() => { Skin?.ViewInExplorer(); }, () => Skin != null));
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
                set => Apply(value, ref _singleSkin);
            }

            private IDarkPreviewsUpdater _previewsUpdater;

            private void PrepareUpdater() {
                var options = Settings.ToPreviewsOptions();
                if (_previewsUpdater == null) {
                    _previewsUpdater = DarkPreviewsUpdaterFactory.Create(SettingsHolder.CustomShowroom.CspPreviewsReady, 
                            AcRootDirectory.Instance.RequireValue, options);
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
                    Renderer.IsPaused = true;

                    var temporary = FilesStorage.Instance.GetTemporaryFilename("Preview test.jpg");
                    var presetName = GetPresetName();
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Generating image…"));
                        await _previewsUpdater.ShotAsync(car.Id, skin.Id, temporary, car.AcdData,
                                GetInformation(car, skin, presetName, Settings.ToPreviewsOptions().GetChecksum(SettingsHolder.CustomShowroom.CspPreviewsReady)),
                                cancellation: waiting.CancellationToken);

                        waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Saving…"));
                        await _previewsUpdater.WaitForProcessing();
                    }

                    var filename = Path.Combine(skin.Location, Settings.FileName);
                    if (new ImageViewer(new[] {
                        temporary,
                        filename
                    }, detailsCallback: i => i == 0 ? "Newly generated preview" : "Current preview") {
                        MaxImageWidth = Settings.Width,
                        MaxImageHeight = Settings.Height
                    }.SelectDialog() == 0) {
                        if (File.Exists(filename)) {
                            FileUtils.Recycle(filename);
                        }

                        File.Move(temporary, filename);
                    }

                    _errors = new UpdatePreviewError[0];
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                    _errors = null;
                } finally {
                    Renderer.IsPaused = false;
                }
            }));

            private AsyncCommand _applyCommand;

            public AsyncCommand ApplyCommand => _applyCommand ?? (_applyCommand = new AsyncCommand(async () => {
                var car = Car;
                var skin = Skin;
                if (car == null || skin == null) throw new Exception("Car or skin are not defined");

                try {
                    Renderer.IsPaused = true;
                    PrepareUpdater();
                    _errors = await UpdatePreviewAsync(new[] {
                        new ToUpdatePreview(car, skin)
                    }, Settings.ToPreviewsOptions(), GetPresetName(), _previewsUpdater);
                } finally {
                    Renderer.IsPaused = false;
                }
            }));

            private AsyncCommand _applyAllCommand;

            public AsyncCommand ApplyAllCommand => _applyAllCommand ?? (_applyAllCommand = new AsyncCommand(async () => {
                var car = Car;
                if (car == null) return;

                try {
                    Renderer.IsPaused = true;
                    PrepareUpdater();
                    _errors = await UpdatePreviewAsync(ToUpdate ?? new[] {
                        new ToUpdatePreview(car)
                    }, Settings.ToPreviewsOptions(), GetPresetName(), _previewsUpdater);
                } finally {
                    Renderer.IsPaused = false;
                }
            }));
            #endregion

            public void Dispose() {
                DisposeHelper.Dispose(ref _previewsUpdater);
            }
        }

        private static string GetPresetName() {
            return Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(CmPreviewsSettingsValues.DefaultPresetableKeyValue));
        }

        private static ImageUtils.ImageInformation GetInformation([CanBeNull] CarObject car, [CanBeNull] CarSkinObject skin, string presetName, string checksum) {
            var result = new ImageUtils.ImageInformation {
                Software = $"ContentManager {BuildInformation.AppVersion}",
                Comment = presetName == null ? $"Settings checksum: {checksum}" : $"Used preset: {presetName} (checksum: {checksum})"
            };

            if (SettingsHolder.CustomShowroom.DetailedExifForPreviews && car != null && skin != null) {
                result.Subject = $"{car.DisplayName}";
                result.Title = $"{car.DisplayName} ({skin.DisplayName})";
                result.Tags = car.Tags.ToArray();
                result.Author = skin.Author ?? car.Author;
            }

            return result;
        }

        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> UpdatePreviewAsync([NotNull] IReadOnlyList<ToUpdatePreview> entries,
                [NotNull] DarkPreviewsOptions options, string presetName = null, IDarkPreviewsUpdater updater = null,
                Func<CarSkinObject, string> destinationOverrideCallback = null, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            return UpdaterFactory(entries, options, presetName, updater, destinationOverrideCallback, progress, cancellation).RunAsync();
        }

        /// <summary>
        /// Last used settings will be used.
        /// </summary>
        [ItemCanBeNull]
        public static Task<IReadOnlyList<UpdatePreviewError>> UpdatePreviewAsync(IReadOnlyList<ToUpdatePreview> entries, string presetFilename = null) {
            try {
                return UpdatePreviewAsync(entries, CmPreviewsSettings.GetSavedOptions(presetFilename) ?? throw new Exception("Can’t load options"),
                        GetPresetName());
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update preview", e);
                return null;
            }
        }

        /// <summary>
        /// Get checksum of specified preset.
        /// </summary>
        [CanBeNull]
        public static string GetChecksum(bool cspRenderMode, string presetFilename = null) {
            return CmPreviewsSettings.GetSavedOptions(presetFilename)?.GetChecksum(cspRenderMode);
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
            Skins = new[] { skin };
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
        Options,
        Start,
        StartManual
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