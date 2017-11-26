using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = System.Windows.Controls.Control;
using FileDialogCustomPlace = Microsoft.Win32.FileDialogCustomPlace;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.CustomShowroom {
    public interface ILiveryGenerator {
        Task CreateLiveryAsync(CarSkinObject skinDirectory, Color[] colors, string preferredStyle);
    }

    public class AmbientShadowParams : NotifyPropertyChanged, IUserPresetable {
        private const string KeyOldSavedData = "__LiteShowroomTools";
        private const string KeySavedData = "__LiteShowroomTools_AS";

        private readonly ISaveHelper _saveable;

        private void SaveLater() {
            _saveable.SaveLater();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public AmbientShadowParams() {
            if (ValuesStorage.Contains(KeyOldSavedData) && !ValuesStorage.Contains(KeySavedData)) {
                ValuesStorage.Set(KeySavedData, ValuesStorage.GetString(KeyOldSavedData));
            }

            _saveable = new SaveHelper<SaveableData>(KeySavedData, () => new SaveableData {
                AmbientShadowDiffusion = Diffusion,
                AmbientShadowBrightness = Brightness,
                AmbientShadowIterations = Iterations,
                AmbientShadowHideWheels = HideWheels,
                AmbientShadowFade = Fade,
                AmbientShadowAccurate = Accurate,
                AmbientShadowBodyMultiplier = BodyMultiplier,
                AmbientShadowWheelMultiplier = WheelMultiplier,
            }, Load);

            _saveable.Initialize();
        }

        public class SaveableData {
            public double AmbientShadowDiffusion = 60d;

            [JsonProperty("asb")]
            public double AmbientShadowBrightness = 200d;

            public int AmbientShadowIterations = 8000;
            public bool AmbientShadowHideWheels;
            public bool AmbientShadowFade = true;

            [JsonProperty("asa")]
            public bool AmbientShadowAccurate = true;

            [JsonProperty("asbm")]
            public float AmbientShadowBodyMultiplier = 0.75f;

            [JsonProperty("aswm")]
            public float AmbientShadowWheelMultiplier = 0.35f;
        }

        /*public static SaveableData LoadAmbientShadowParams() {
            if (!ValuesStorage.Contains(KeySavedData)) {
                return new SaveableData();
            }

            try {
                return JsonConvert.DeserializeObject<SaveableData>(ValuesStorage.GetString(KeySavedData));
            } catch (Exception e) {
                Logging.Warning(e);
                return new SaveableData();
            }
        }*/

        private void Load(SaveableData o) {
            Diffusion = o.AmbientShadowDiffusion;
            Brightness = o.AmbientShadowBrightness;
            Iterations = o.AmbientShadowIterations;
            HideWheels = o.AmbientShadowHideWheels;
            Fade = o.AmbientShadowFade;
            Accurate = o.AmbientShadowAccurate;
            BodyMultiplier = o.AmbientShadowBodyMultiplier;
            WheelMultiplier = o.AmbientShadowWheelMultiplier;
        }

        private double _diffusion;

        public double Diffusion {
            get => _diffusion;
            set {
                value = value.Clamp(0.0, 100.0);
                if (Equals(value, _diffusion)) return;
                _diffusion = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _brightness;

        public double Brightness {
            get => _brightness;
            set {
                value = value.Clamp(150.0, 800.0);
                if (Equals(value, _brightness)) return;
                _brightness = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _iterations;

        public int Iterations {
            get => _iterations;
            set {
                value = value.Clamp(400, 40000);
                if (Equals(value, _iterations)) return;
                _iterations = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _hideWheels;

        public bool HideWheels {
            get => _hideWheels;
            set {
                if (Equals(value, _hideWheels)) return;
                _hideWheels = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _fade;

        public bool Fade {
            get => _fade;
            set {
                if (Equals(value, _fade)) return;
                _fade = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _accurate;

        public bool Accurate {
            get => _accurate;
            set {
                if (Equals(value, _accurate)) return;
                _accurate = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private float _bodyMultiplier = 0.8f;

        public float BodyMultiplier {
            get => _bodyMultiplier;
            set {
                if (Equals(value, _bodyMultiplier)) return;
                _bodyMultiplier = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private float _wheelMultiplier = 0.69f;

        public float WheelMultiplier {
            get => _wheelMultiplier;
            set {
                if (Equals(value, _wheelMultiplier)) return;
                _wheelMultiplier = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        /*private void Reset(bool saveLater) {
            Load(new SaveableData());
            if (saveLater) {
                SaveLater();
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            Reset(true);
        }));*/

        #region Presets
        public bool CanBeSaved => true;
        public string PresetableKey => KeySavedData;
        public PresetsCategory PresetableCategory => new PresetsCategory("Ambient Shadows");

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }
        #endregion

        /*#region Share
        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        protected async Task Share() {
            var data = ExportToPresetData();
            if (data == null) return;
            await SharingUiHelper.ShareAsync(SharedEntryType.CustomShowroomPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(KeySavedData)), null,
                    data);
        }
        #endregion*/
    }

    public class AmbientShadowViewModel : AmbientShadowParams {
        [NotNull]
        private readonly CarObject _car;

        [NotNull]
        private readonly ToolsKn5ObjectRenderer _renderer;

        public AmbientShadowViewModel([NotNull] ToolsKn5ObjectRenderer renderer, [NotNull] CarObject car) {
            _car = car ?? throw new ArgumentNullException(nameof(car));
            _renderer = renderer;
            renderer.SubscribeWeak(OnPropertyChanged);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(_renderer.AmbientShadowSizeChanged):
                    ActionExtension.InvokeInMainThread(() => {
                        _sizeSaveCommand?.RaiseCanExecuteChanged();
                        _sizeResetCommand?.RaiseCanExecuteChanged();
                    });
                    break;
            }
        }

        /*private static string ToString(Vector3 vec) {
            return $"{-vec.X:F3}, {vec.Y:F3}, {vec.Z:F3}";
        }*/

        private AsyncCommand _updateAmbientShadowCommand;

        public AsyncCommand UpdateCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async () => {
            if (_renderer.AmbientShadowSizeChanged) {
                SizeSaveCommand.Execute();
                if (_renderer.AmbientShadowSizeChanged) return;
            }

            try {
                using (var waiting = WaitingDialog.Create(ControlsStrings.CustomShowroom_AmbientShadows_Updating)) {
                    var cancellation = waiting.CancellationToken;
                    var progress = (IProgress<double>)waiting;

                    await Task.Run(() => {
                        var kn5 = _renderer.MainSlot.Kn5;
                        if (kn5 == null) return;

                        using (var renderer = new AmbientShadowRenderer(kn5, _car.AcdData) {
                            DiffusionLevel = (float)Diffusion / 100f,
                            SkyBrightnessLevel = (float)Brightness / 100f,
                            Iterations = Iterations,
                            HideWheels = HideWheels,
                            Fade = Fade,
                            CorrectLighting = Accurate,
                            BodyMultipler = BodyMultiplier,
                            WheelMultipler = WheelMultiplier,
                        }) {
                            renderer.CopyStateFrom(_renderer);
                            renderer.Initialize();
                            renderer.Shot(progress, cancellation);
                        }
                    });

                    waiting.Report(ControlsStrings.CustomShowroom_AmbientShadows_Reloading);
                }
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
            }
        }));

        private DelegateCommand _sizeSaveCommand;

        public DelegateCommand SizeSaveCommand => _sizeSaveCommand ?? (_sizeSaveCommand = new DelegateCommand(() => {
            if (File.Exists(Path.Combine(_car.Location, "data.acd")) && ModernDialog.ShowMessage(
                    ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedDataMessage,
                    ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedData, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var data = Path.Combine(_car.Location, "data");
            Directory.CreateDirectory(data);
            new IniFile {
                ["SETTINGS"] = {
                    ["WIDTH"] = _renderer.AmbientShadowWidth,
                    ["LENGTH"] = _renderer.AmbientShadowLength,
                }
            }.Save(Path.Combine(data, "ambient_shadows.ini"));

            _renderer.AmbientShadowSizeChanged = false;
        }, () => _renderer.AmbientShadowSizeChanged));

        private DelegateCommand _sizeFitCommand;

        public DelegateCommand SizeFitCommand => _sizeFitCommand ?? (_sizeFitCommand = new DelegateCommand(() => {
            _renderer.FitAmbientShadowSize();
        }));

        private DelegateCommand _sizeResetCommand;

        public DelegateCommand SizeResetCommand => _sizeResetCommand ?? (_sizeResetCommand = new DelegateCommand(() => {
            _renderer.ResetAmbientShadowSize();
        }, () => _renderer.AmbientShadowSizeChanged));
    }

    public partial class LiteShowroomTools {
        private const string KeySavedData = "__LiteShowroomTools";

        public static ILiveryGenerator LiveryGenerator { get; set; }

        private readonly string _loadPreset;

        public LiteShowroomTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, [CanBeNull] string loadPreset, ICustomShowroomShots shots) {
            _loadPreset = loadPreset;

            DataContext = new ViewModel(renderer, car, skinId, shots);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.PreviewSkinCommand, new KeyGesture(Key.PageUp)),
                new InputBinding(Model.NextSkinCommand, new KeyGesture(Key.PageDown)),
                new InputBinding(Model.Car.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
                new InputBinding(Model.OpenSkinDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => Model.Renderer?.Deselect()), new KeyGesture(Key.D, ModifierKeys.Control))
            });
            InitializeComponent();
            Buttons = new Button[0];
            this.OnActualUnload(() => Model.Dispose());
        }

        protected override void OnKeyUp(KeyEventArgs e) {
            if (!(Keyboard.FocusedElement is TextBoxBase) && !(Keyboard.FocusedElement is CheckBox) &&
                    Model.Mode != Mode.Main && (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack)) {
                Model.Mode = Mode.Main;
                e.Handled = true;
            }

            base.OnKeyUp(e);
        }

        /*protected override void OnKeyDown(KeyEventArgs e) {
        //
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack) {
                Model.Mode = Mode.Main;
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }*/

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            var saveable = Model.Settings;
            if (saveable == null) return;

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

        public ViewModel Model => (ViewModel)DataContext;

        public enum Mode {
            Main,
            VisualSettings,
            Selected,
            AmbientShadows,
            Car,
            Cars,
            Skin,
            Camera,
            Shot
        }

        public bool CanSelectNodes => Model.CanSelectNodes();

        public partial class ViewModel : NotifyPropertyChanged, IDisposable {
            private Mode _mode = Mode.Main;
            private bool _ignoreModeChange;

            public Mode Mode {
                get => _mode;
                set {
                    if (Equals(value, _mode) || _ignoreModeChange) return;

                    var renderer = Renderer;
                    if (_mode == Mode.AmbientShadows && renderer != null) {
                        renderer.AmbientShadowHighlight = false;
                    }

                    if (value != Mode.Skin) {
                        DisposeSkinItems();
                    }

                    if (value == Mode.Skin && SkinItems == null) {
                        if (!PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId)) {
                            NonfatalError.Notify("Can’t edit skins without Magick.NET plugin", "Please, go to Settings/Plugins and install it first.");
                            value = Mode.Main;
                        /*} else {
                            LoadSkinItems();*/
                        }
                    }

                    _mode = value;

                    if (renderer?.SelectedObject != null && Mode != Mode.Selected) {
                        try {
                            _ignoreModeChange = true;
                            renderer.SelectedObject = null;
                        } finally {
                            _ignoreModeChange = false;
                        }
                    }

                    OnPropertyChanged();
                }
            }

            public bool CanSelectNodes() => Mode == Mode.Main || Mode == Mode.Selected ||
                    (Renderer as DarkKn5ObjectRenderer)?.Lights.Any(x => x.Tag.IsCarTag && x.AttachedToSelect) == true;

            private DelegateCommand<Mode> _selectModeCommand;

            public DelegateCommand<Mode> SelectModeCommand => _selectModeCommand ?? (_selectModeCommand = new DelegateCommand<Mode>(m => {
                Mode = m;
            }));

            private DelegateCommand _transferToCmPreviewsCommand;

            public DelegateCommand TransferToCmPreviewsCommand => _transferToCmPreviewsCommand ?? (_transferToCmPreviewsCommand = new DelegateCommand(() => {
                if (Settings != null && Renderer is DarkKn5ObjectRenderer darkRenderer) {
                    CmPreviewsSettings.Transfer(Settings, darkRenderer);
                }
            }));

            private ToolsKn5ObjectRenderer _renderer;

            // Set to NULL when View Model is disposed and control is unloaded.
            [CanBeNull]
            public ToolsKn5ObjectRenderer Renderer {
                get => _renderer;
                private set {
                    if (Equals(value, _renderer)) return;
                    _renderer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DarkRenderer));

                    AmbientShadow = value == null ? null : new AmbientShadowViewModel(value, Car);

                    var dark = value as DarkKn5ObjectRenderer;
                    Settings = dark != null ? new DarkRendererSettings(dark) : null;
                    Cars = dark != null ? new DarkRendererCars(dark) : null;
                }
            }

            // Set to NULL when View Model is disposed and control is unloaded
            private AmbientShadowViewModel _ambientShadow;

            [CanBeNull]
            public AmbientShadowViewModel AmbientShadow {
                get => _ambientShadow;
                private set {
                    if (Equals(value, _ambientShadow)) return;
                    _ambientShadow = value;
                    OnPropertyChanged();
                }
            }

            // Not always renderer is the Dark one
            [CanBeNull]
            public DarkKn5ObjectRenderer DarkRenderer => _renderer as DarkKn5ObjectRenderer;

            private DarkRendererSettings _settings;

            // Set to NULL if renderer is not the Dark one
            [CanBeNull]
            public DarkRendererSettings Settings {
                get => _settings;
                private set {
                    if (Equals(value, _settings)) return;
                    _settings = value;
                    OnPropertyChanged();
                }
            }

            private DarkRendererCars _cars;

            public DarkRendererCars Cars {
                get => _cars;
                set {
                    if (Equals(value, _cars)) return;
                    _cars = value;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _addCarCommand;
            private CarObject _selectedCarToAdd;
            private CarSkinObject _selectedCarSkinToAdd;

            public DelegateCommand AddCarCommand => _addCarCommand ?? (_addCarCommand = new DelegateCommand(() => {
                var car = SelectCarDialog.Show(_selectedCarToAdd ?? CarsManager.Instance.GetDefault(), ref _selectedCarSkinToAdd);
                if (car == null) return;

                _selectedCarToAdd = car;
                var slot = DarkRenderer?.AddCar(CarDescription.FromDirectory(car.Location, car.AcdData));
                if (slot != null && _selectedCarSkinToAdd != null) {
                    slot.SelectSkin(_selectedCarSkinToAdd?.Id);
                }
            }));

            public bool MagickNetEnabled => PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId);

            [NotNull]
            public CarObject Car { get; }

            private CarSkinObject _skin;

            public CarSkinObject Skin {
                get => _skin;
                set {
                    if (Equals(value, _skin)) return;
                    _skin = value;
                    OnPropertyChanged();

                    Renderer?.SelectSkin(value?.Id);
                }
            }

            private class SaveableData {
                public bool LiveReload;
                [JsonProperty("cp")] public double[] CameraPosition = { 3.194, 0.342, 13.049 };
                [JsonProperty("cl")] public double[] CameraLookAt = { 0, 0, 0 };
                [JsonProperty("ti")] public float CameraTilt;
                [JsonProperty("cf")] public float CameraFov = 36f;
                [JsonProperty("co")] public bool CameraOrbit = true;
                [JsonProperty("cr")] public bool CameraAutoRotate = true;
                [JsonProperty("cd")] public double CameraAutoRotateSpeed = 1d;
                [JsonProperty("cg")] public bool CameraAutoAdjustTarget = true;
                [JsonProperty("sw")] public int ShotWidth = 1920;
                [JsonProperty("sh")] public int ShotHeight = 1080;
                [JsonProperty("sm")] public int ShotSizeMultiplier = 1;
                [JsonProperty("sf")] public int ShotFormat = (int)RendererShotFormat.Jpeg;
                [JsonProperty("sd")] public bool ShotDownsizeInTwo = true;
            }

            protected ISaveHelper Saveable { get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            private readonly ICustomShowroomShots _shots;

            public ViewModel([NotNull] ToolsKn5ObjectRenderer renderer, [NotNull] CarObject carObject, [CanBeNull] string skinId, ICustomShowroomShots shots) {
                Car = carObject;
                _shots = shots;

                Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
                renderer.PropertyChanged += OnRendererPropertyChanged;
                renderer.CameraMoved += OnCameraMoved;
                OnCarNodeUpdated();

                CameraLookAt.PropertyChanged += OnCameraCoordinatesChanged;
                CameraPosition.PropertyChanged += OnCameraCoordinatesChanged;

                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>(KeySavedData, () => new SaveableData {
                    LiveReload = renderer.MagickOverride,

                    CameraPosition = CameraPosition.ToArray(),
                    CameraLookAt = CameraLookAt.ToArray(),
                    CameraTilt = CameraTilt,
                    CameraFov = CameraFov,
                    CameraOrbit = CameraOrbit,
                    CameraAutoRotate = CameraAutoRotate,
                    CameraAutoRotateSpeed = CameraAutoRotateSpeed,
                    CameraAutoAdjustTarget = CameraAutoAdjustTarget,

                    ShotWidth = ShotWidth,
                    ShotHeight = ShotHeight,
                    ShotSizeMultiplier = ShotSizeMultiplier,
                    ShotFormat = ShotFormat.IntValue ?? 0,
                    ShotDownsizeInTwo = ShotDownsizeInTwo,
                }, Load);

                Saveable.Initialize();
                PaintShopSupported = Lazier.CreateAsync(async () => (await CmApiProvider.GetPaintShopIdsAsync())?.Contains(Car.Id) == true);

                var defaultSize = _shots.DefaultSize;
                ShotWidth = defaultSize.Width;
                ShotHeight = defaultSize.Height;

                _shots.PreviewScreenshot += OnScreenshot;
            }

            private void OnScreenshot(object sender, CancelEventArgs cancelEventArgs) {
                if (Keyboard.Modifiers == ModifierKeys.None) {
                    cancelEventArgs.Cancel = true;
                    ShotCommand.Execute(null);
                }
            }

            #region Shots
            public bool ShotSplitMode => ShotSizeMultiplier > (ShotDownsizeInTwo ? 2 : 1);
            public bool ShotMagickWarning => !ImageUtils.IsMagickSupported;
            public bool ShotMagickMontageWarning => ShotSplitMode && !PluginsManager.Instance.IsPluginEnabled("ImageMontage");

            private int _shotWidth;

            public int ShotWidth {
                get => _shotWidth;
                set {
                    value = value.Clamp(16, 3860);
                    if (Equals(value, _shotWidth)) return;
                    _shotWidth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayShotSize));
                    SaveLater();
                }
            }

            private int _shotHeight;

            public int ShotHeight {
                get => _shotHeight;
                set {
                    value = value.Clamp(16, 2160);
                    if (Equals(value, _shotHeight)) return;
                    _shotHeight = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayShotSize));
                    SaveLater();
                }
            }

            private DelegateCommand<string> _shotSetResolutionCommand;

            public DelegateCommand<string> ShotSetResolutionCommand => _shotSetResolutionCommand ?? (_shotSetResolutionCommand = new DelegateCommand<string>(s => {
                var d = s?.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (d?.Length == 2) {
                    ShotWidth = d[0].AsInt();
                    ShotHeight = d[1].AsInt();
                }
            }));

            public string DisplayShotSize => $"{ShotWidth * ShotSizeMultiplier}×{ShotHeight * ShotSizeMultiplier}";

            private int _shotSizeMultiplier = 1;

            public int ShotSizeMultiplier {
                get => _shotSizeMultiplier;
                set {
                    value = value.Clamp(1, 100);
                    if (Equals(value, _shotSizeMultiplier)) return;
                    _shotSizeMultiplier = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayShotSize));
                    OnPropertyChanged(nameof(ShotSplitMode));
                    OnPropertyChanged(nameof(ShotMagickMontageWarning));
                    SaveLater();
                }
            }

            private bool _shotDownsizeInTwo = true;

            public bool ShotDownsizeInTwo {
                get => _shotDownsizeInTwo;
                set {
                    if (Equals(value, _shotDownsizeInTwo)) return;
                    _shotDownsizeInTwo = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShotSplitMode));
                    OnPropertyChanged(nameof(ShotMagickMontageWarning));
                    SaveLater();
                }
            }

            private SettingEntry _shotFormat = ShotFormatJpeg;

            public SettingEntry ShotFormat {
                get => _shotFormat;
                set {
                    if (Equals(value, _shotFormat)) return;
                    _shotFormat = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public static readonly SettingEntry ShotFormatJpeg = new SettingEntry((int)RendererShotFormat.Jpeg, "JPEG");

            public static readonly SettingEntry ShotFormatPng = new SettingEntry((int)RendererShotFormat.Png, "PNG") {
                Tag = "Without showroom selected, will produce a semi-transparent screenshot"
            };

            public static readonly SettingEntry ShotFormatHdrExr = new SettingEntry((int)RendererShotFormat.HdrExr, "HDR (OpenEXR)") {
                Tag = "Tone mapping, color mapping and dithering don’t work in HDR mode. Resolution multiplier is not available."
            };

            public SettingEntry[] ShotFormats { get; } = {
                ShotFormatJpeg,
                ShotFormatPng,
                ShotFormatHdrExr
            };

            private static readonly Stored.StoredValue LastShot = Stored.Get("customShowroom.lastShot", null);

            private DelegateCommand _shotOpenDirectoryCommand;

            public DelegateCommand ShotOpenDirectoryCommand => _shotOpenDirectoryCommand ?? (_shotOpenDirectoryCommand = new DelegateCommand(() => {
                if (LastShot.Value != null && File.Exists(LastShot.Value)) {
                    WindowsHelper.ViewFile(LastShot.Value);
                } else {
                    WindowsHelper.ViewDirectory(AcPaths.GetDocumentsScreensDirectory());
                }
            }));

            private AsyncCommand<CancellationToken?> _shotCommand;

            public AsyncCommand<CancellationToken?> ShotCommand => _shotCommand ?? (_shotCommand = new AsyncCommand<CancellationToken?>(c => {
                var format = (RendererShotFormat)(ShotFormat.IntValue ?? 0);
                Logging.Debug($"format: {ShotFormat}, {ShotFormat.IntValue ?? -1}, {format}");

                var downscale = ShotDownsizeInTwo;
                var multiplier = ShotSizeMultiplier * (downscale ? 2 : 1);
                var directory = AcPaths.GetDocumentsScreensDirectory();
                FileUtils.EnsureDirectoryExists(directory);

                var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}{format.GetExtension()}");
                LastShot.Value = filename;

                if (ImageUtils.IsMagickSupported && ShotSplitMode) {
                    var size = new System.Drawing.Size(ShotWidth.Round(4) * multiplier, ShotHeight.Round(4) * multiplier);
                    return _shots.SplitShotAsync(size, downscale, filename, format, c ?? default(CancellationToken));
                } else {
                    var size = new System.Drawing.Size(ShotWidth * multiplier, ShotHeight * multiplier);
                    return _shots.ShotAsync(size, downscale, filename, format, c ?? default(CancellationToken));
                }
            }));
            #endregion

            #region Camera
            public Coordinates CameraPosition { get; } = new Coordinates();

            public Coordinates CameraLookAt { get; } = new Coordinates();

            private void OnCameraCoordinatesChanged(object sender, PropertyChangedEventArgs e) {
                SaveLater();
                UpdateCamera();
            }

            private float _cameraFov;

            public float CameraFov {
                get => _cameraFov;
                set {
                    if (Equals(value, _cameraFov)) return;
                    _cameraFov = value;
                    OnPropertyChanged();
                    UpdateCamera();
                    SaveLater();
                }
            }

            private float _cameraTilt;

            public float CameraTilt {
                get => _cameraTilt;
                set {
                    if (Equals(value, _cameraTilt)) return;
                    _cameraTilt = value;
                    OnPropertyChanged();
                    UpdateCamera();
                    SaveLater();
                }
            }

            private bool _cameraOrbit;

            public bool CameraOrbit {
                get => _cameraOrbit;
                set {
                    if (Equals(value, _cameraOrbit)) return;
                    _cameraOrbit = value;
                    OnPropertyChanged();
                    UpdateCamera();
                    SaveLater();
                }
            }

            private bool _cameraAutoRotate;

            public bool CameraAutoRotate {
                get => _cameraAutoRotate;
                set {
                    if (Equals(value, _cameraAutoRotate)) return;
                    _cameraAutoRotate = value;

                    if (!_cameraBusy && Renderer != null) {
                        Renderer.AutoRotate = value;
                    }

                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _cameraAutoRotateSpeed;

            public double CameraAutoRotateSpeed {
                get => _cameraAutoRotateSpeed;
                set {
                    if (Equals(value, _cameraAutoRotateSpeed)) return;
                    _cameraAutoRotateSpeed = value;

                    if (!_cameraBusy && Renderer != null) {
                        Renderer.AutoRotateSpeed = (float)value;
                    }

                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _cameraAutoAdjustTarget;

            public bool CameraAutoAdjustTarget {
                get => _cameraAutoAdjustTarget;
                set {
                    if (Equals(value, _cameraAutoAdjustTarget)) return;
                    _cameraAutoAdjustTarget = value;

                    if (!_cameraBusy && Renderer != null) {
                        Renderer.AutoAdjustTarget = value;
                    }

                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private DelegateCommand _resetCameraCommand;

            public DelegateCommand ResetCameraCommand => _resetCameraCommand ?? (_resetCameraCommand = new DelegateCommand(() => {
                Renderer?.ResetCamera();
            }));

            private DelegateCommand _loadPresetCameraCommand;

            public DelegateCommand LoadPresetCameraCommand => _loadPresetCameraCommand ?? (_loadPresetCameraCommand = new DelegateCommand(() => {
                try {
                    var showroomPresetsDirectory = PresetsManager.Instance.EnsureDirectory(new PresetsCategory(DarkRendererSettings.DefaultPresetableKeyValue));
                    var previewsPresetsDirectory = PresetsManager.Instance.EnsureDirectory(new PresetsCategory(CmPreviewsSettings.DefaultPresetableKeyValue));

                    var dialog = new OpenFileDialog {
                        InitialDirectory = showroomPresetsDirectory,
                        Filter = string.Format(ToolsStrings.Presets_FileFilter, PresetsCategory.DefaultFileExtension),
                        Title = "Select Custom Showroom Or Custom Previews Preset",
                        CustomPlaces = {
                            new FileDialogCustomPlace(showroomPresetsDirectory),
                            new FileDialogCustomPlace(previewsPresetsDirectory),
                        }
                    };

                    if (dialog.ShowDialog() == true) {
                        Settings?.LoadCamera(File.ReadAllText(dialog.FileName));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load camera from preset", e);
                }
            }, () => Settings != null));

            private bool _cameraBusy;

            private void OnCameraMoved(object sender, EventArgs e) {
                SyncCamera();
            }

            private void SyncCamera() {
                var renderer = Renderer;
                if (renderer == null || _cameraBusy) return;
                _cameraBusy = true;

                try {
                    CameraPosition.Set(renderer.Camera.Position);
                    CameraLookAt.Set(renderer.CameraOrbit?.Target ?? renderer.Camera.Position + renderer.Camera.Look);
                    CameraTilt = renderer.Camera.Tilt.ToDegrees();
                    CameraFov = renderer.Camera.FovY.ToDegrees();
                    CameraOrbit = renderer.CameraOrbit != null;
                } finally {
                    _cameraBusy = false;
                }
            }

            private void UpdateCamera() {
                var renderer = Renderer;
                if (renderer == null || _cameraBusy) return;
                _cameraBusy = true;

                try {
                    if (CameraOrbit) {
                        renderer.SetCameraOrbit(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians(), CameraTilt.ToRadians());
                    } else {
                        renderer.SetCamera(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians(), CameraTilt.ToRadians());
                    }

                    renderer.AutoRotate = CameraAutoRotate;
                    renderer.AutoRotateSpeed = (float)CameraAutoRotateSpeed;
                    renderer.AutoAdjustTarget = CameraAutoAdjustTarget;
                } finally {
                    _cameraBusy = false;
                }
            }
            #endregion

            private void Load(SaveableData o) {
                if (Renderer != null) {
                    Renderer.MagickOverride = o.LiveReload;
                }

                _cameraBusy = true;
                try {
                    CameraPosition.Set(o.CameraPosition);
                    CameraLookAt.Set(o.CameraLookAt);
                    CameraTilt = o.CameraTilt;
                    CameraFov = o.CameraFov;
                    CameraOrbit = o.CameraOrbit;
                    CameraAutoRotate = o.CameraAutoRotate;
                    CameraAutoRotateSpeed = o.CameraAutoRotateSpeed;
                    CameraAutoAdjustTarget = o.CameraAutoAdjustTarget;
                } finally {
                    _cameraBusy = false;
                    UpdateCamera();
                }

                ShotWidth = o.ShotWidth;
                ShotHeight = o.ShotHeight;
                ShotSizeMultiplier = o.ShotSizeMultiplier;
                ShotFormat = ShotFormats.GetByIdOrDefault((int?)o.ShotFormat) ?? ShotFormatJpeg;
                ShotDownsizeInTwo = o.ShotDownsizeInTwo;
            }

            /*private void Reset(bool saveLater) {
                Load(new SaveableData());
                if (saveLater) {
                    SaveLater();
                }
            }*/

            private INotifyPropertyChanged _carNode;

            private void OnCarNodeUpdated() {
                if (_carNode != null) {
                    _carNode.PropertyChanged -= OnCarNodePropertyChanged;
                }
                _carNode = _renderer.CarNode;
                if (_carNode != null) {
                    _carNode.PropertyChanged += OnCarNodePropertyChanged;
                }
            }

            private void OnCarNodePropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.CarNode.CurrentSkin):
                        Skin = Car.GetSkinById(Renderer?.CarNode?.CurrentSkin ?? "");
                        break;
                }
            }

            private int _selectedObjectTrianglesCount;

            public int SelectedObjectTrianglesCount {
                get => _selectedObjectTrianglesCount;
                set {
                    if (Equals(value, _selectedObjectTrianglesCount)) return;
                    _selectedObjectTrianglesCount = value;
                    OnPropertyChanged();
                }
            }

            private void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.MagickOverride):
                        ActionExtension.InvokeInMainThread(SaveLater);
                        break;

                    case nameof(Renderer.CarNode):
                        ActionExtension.InvokeInMainThread(OnCarNodeUpdated);
                        break;

                    case nameof(Renderer.AutoAdjustTarget):
                        CameraAutoAdjustTarget = Renderer?.AutoAdjustTarget != false;
                        break;

                    case nameof(Renderer.AutoRotate):
                        CameraAutoRotate = Renderer?.AutoRotate != false;
                        break;

                    case nameof(Renderer.AutoRotateSpeed):
                        CameraAutoRotateSpeed = Renderer?.AutoRotateSpeed ?? 1d;
                        break;

                    case nameof(Renderer.SelectedObject):
                        ActionExtension.InvokeInMainThread(() => {
                            Mode = Renderer?.SelectedObject != null ? Mode.Selected : Mode.Main;
                            SelectedObjectTrianglesCount = Renderer?.SelectedObject?.TrianglesCount ?? 0;
                            _viewObjectCommand?.RaiseCanExecuteChanged();
                        });
                        break;

                    case nameof(Renderer.SelectedMaterial):
                        ActionExtension.InvokeInMainThread(() => {
                            _viewMaterialCommand?.RaiseCanExecuteChanged();
                        });
                        break;
                }
            }

            private DelegateCommand _toggleAmbientShadowModeCommand;

            public DelegateCommand ToggleAmbientShadowModeCommand
                => _toggleAmbientShadowModeCommand ?? (_toggleAmbientShadowModeCommand = new DelegateCommand(() => {
                    Mode = Mode == Mode.AmbientShadows ? Mode.Main : Mode.AmbientShadows;
                }));

            private DelegateCommand _mainModeCommand;

            public DelegateCommand MainModeCommand => _mainModeCommand ?? (_mainModeCommand = new DelegateCommand(() => {
                Mode = Mode.Main;
            }));

            private AsyncCommand _updateKn5Command;

            public AsyncCommand UpdateKn5Command => _updateKn5Command ?? (_updateKn5Command = new AsyncCommand(async () => {
                var renderer = Renderer;
                var kn5 = renderer?.CarNode?.GetCurrentLodKn5();
                if (kn5 == null) return;

                using (WaitingDialog.Create("Updating model…")) {
                    renderer.CarNode.UpdateCurrentLodKn5Values();
                    await kn5.UpdateKn5(_renderer, _skin);
                }
            }));

            #region Commands
            private DelegateCommand _nextSkinCommand;

            public DelegateCommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new DelegateCommand(() => {
                Renderer?.SelectNextSkin();
            }));

            private DelegateCommand _previewSkinCommand;

            public DelegateCommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new DelegateCommand(() => {
                Renderer?.SelectPreviousSkin();
            }));

            private DelegateCommand _openSkinDirectoryCommand;

            public DelegateCommand OpenSkinDirectoryCommand => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new DelegateCommand(() => {
                Skin.ViewInExplorer();
            }, () => Skin != null));

            private AsyncCommand _unpackKn5Command;

            public AsyncCommand UnpackKn5Command => _unpackKn5Command ?? (_unpackKn5Command = new AsyncCommand(async () => {
                var kn5 = Renderer?.MainSlot.Kn5;
                if (kn5 == null) return;

                try {
                    Kn5.FbxConverterLocation = PluginsManager.Instance.GetPluginFilename("FbxConverter", "FbxConverter.exe");

                    var destination = Path.Combine(Car.Location, "unpacked");
                    using (var waiting = new WaitingDialog(ToolsStrings.Common_Exporting)) {
                        await Task.Delay(1);

                        if (FileUtils.Exists(destination) &&
                                ModernDialog.ShowMessage(string.Format(ControlsStrings.Common_FolderExists, @"unpacked"), ToolsStrings.Common_Destination,
                                        MessageBoxButton.YesNo) == MessageBoxResult.No) {
                            var temp = destination;
                            var i = 1;
                            do {
                                destination = $"{temp}-{i++}";
                            } while (FileUtils.Exists(destination));
                        }

                        var name = kn5.RootNode.Name.StartsWith(@"FBX: ") ? kn5.RootNode.Name.Substring(5) :
                                @"model.fbx";
                        Directory.CreateDirectory(destination);
                        await kn5.ExportFbxWithIniAsync(Path.Combine(destination, name), waiting, waiting.CancellationToken);

                        var textures = Path.Combine(destination, "texture");
                        Directory.CreateDirectory(textures);
                        await kn5.ExportTexturesAsync(textures, waiting, waiting.CancellationToken);
                    }

                    Process.Start(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(string.Format(ToolsStrings.Common_CannotUnpack, ToolsStrings.Common_KN5), e);
                }
            }, () => SettingsHolder.Common.MsMode && PluginsManager.Instance.IsPluginEnabled("FbxConverter") && Renderer?.MainSlot.Kn5 != null));
            #endregion

            #region Materials & Textures
            private void ShowMessage(string text, string title, Func<ModernDialog, IEnumerable<Control>> buttons = null) {
                var dlg = new ModernDialog {
                    Title = title,
                    Content = new ScrollViewer {
                        Content = new SelectableBbCodeBlock {
                            BbCode = text
                        },
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    },
                    MinHeight = 0,
                    MinWidth = 0,
                    MaxHeight = 640,
                    SizeToContent = SizeToContent.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Width = 480,
                    MaxWidth = 640
                };

                dlg.Buttons = buttons?.Invoke(dlg).Append(dlg.OkButton) ?? new[]{ dlg.OkButton };
                AttachedHelper.GetInstance(_renderer)?.Attach(text, dlg);
            }

            private AsyncCommand _viewObjectCommand;

            public AsyncCommand ViewObjectCommand => _viewObjectCommand ?? (_viewObjectCommand = new AsyncCommand(async () => {
                var obj = Renderer?.SelectedObject;
                if (obj == null) return;

                var attached = AttachedHelper.GetInstance(_renderer);
                if (attached == null) return;

                try {
                    if (Renderer == null) return;
                    await attached.AttachAndWaitAsync("Kn5ObjectDialog",
                            new Kn5ObjectDialog(Renderer, Car, Skin, Renderer.GetKn5(Renderer.SelectedObject), obj));
                } catch (Exception e) {
                    NonfatalError.Notify("Unexpected exception", e);
                }
            }, () => Renderer?.SelectedObject != null));

            private static string MaterialPropertyToString(Kn5Material.ShaderProperty property) {
                if (property.ValueD.Any(x => !Equals(x, 0f))) return $"({property.ValueD.JoinToString(@", ")})";
                if (property.ValueC.Any(x => !Equals(x, 0f))) return $"({property.ValueC.JoinToString(@", ")})";
                if (property.ValueB.Any(x => !Equals(x, 0f))) return $"({property.ValueB.JoinToString(@", ")})";
                return property.ValueA.ToInvariantString();
            }

            private DelegateCommand _viewMaterialCommand;

            public DelegateCommand ViewMaterialCommand => _viewMaterialCommand ?? (_viewMaterialCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers == ModifierKeys.Control) {
                    ChangeMaterialCommand.Execute();
                    return;
                }

                var material = Renderer?.SelectedMaterial;
                if (material == null) return;

                var sb = new StringBuilder();
                sb.Append(string.Format(ControlsStrings.CustomShowroom_MaterialInformation, material.Name, material.ShaderName,
                        material.BlendMode.GetDescription(), material.AlphaTested, material.DepthMode.GetDescription()));

                if (material.ShaderProperties.Any()) {
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append(ControlsStrings.CustomShowroom_ShaderProperties);
                    sb.Append('\n');
                    sb.Append(material.ShaderProperties.Select(x => $"    • {x.Name}: [b]{MaterialPropertyToString(x)}[/b]").JoinToString('\n'));
                }

                if (material.TextureMappings.Any()) {
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append(ControlsStrings.CustomShowroom_Selected_TexturesLabel);
                    sb.Append('\n');
                    sb.Append(material.TextureMappings.Select(x => $"    • {x.Name}: [b]{x.Texture}[/b]").JoinToString('\n'));
                }

                ShowMessage(sb.ToString(), material.Name, d => new [] {
                    d.CreateCloseDialogButton("Change Values", false, false, MessageBoxResult.OK, ChangeMaterialCommand)
                });
            }, () => Renderer?.SelectedMaterial != null));

            private DelegateCommand _changeMaterialCommand;

            public DelegateCommand ChangeMaterialCommand => _changeMaterialCommand ?? (_changeMaterialCommand = new DelegateCommand(async () => {
                try {
                    var attached = AttachedHelper.GetInstance(_renderer);
                    if (attached == null) return;

                    var material = Renderer?.SelectedMaterial;
                    if (material == null) return;

                    using (var dialog = new Kn5MaterialDialog(Renderer, Car, Skin, Renderer.GetKn5(Renderer.SelectedObject),
                            Renderer.SelectedObject?.OriginalNode.MaterialId ?? uint.MaxValue)) {
                        await attached.AttachAndWaitAsync("Kn5MaterialDialog", dialog);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Unexpected exception", e);
                }
            }));

            private DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation> _viewTextureCommand;

            public DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation> ViewTextureCommand
                => _viewTextureCommand ?? (_viewTextureCommand = new DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation>(async o => {
                    var attached = AttachedHelper.GetInstance(_renderer);
                    if (attached == null) return;

                    try {
                        if (Renderer == null) return;
                        await attached.AttachAndWaitAsync("Kn5TextureDialog",
                                new Kn5TextureDialog(Renderer, Car, Skin, Renderer.GetKn5(Renderer.SelectedObject), o.TextureName,
                                        Renderer.SelectedObject?.OriginalNode.MaterialId ?? uint.MaxValue, o.SlotName));
                    } catch (Exception e) {
                        NonfatalError.Notify("Unexpected exception", e);
                    }
                }, o => o != null));
            #endregion

            public void Dispose() {
                Renderer = null;
                DisposeSkinItems();
            }
        }
    }
}
