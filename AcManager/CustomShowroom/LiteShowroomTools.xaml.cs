using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlimDX;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.CustomShowroom {
    public interface ILiveryGenerator {
        Task CreateLiveryAsync(CarSkinObject skinDirectory, Color[] colors, string preferredStyle);
    }

    public partial class LiteShowroomTools {
        private const string KeySavedData = "__LiteShowroomTools";

        public static ILiveryGenerator LiveryGenerator { get; set; }

        private readonly string _loadPreset;

        public LiteShowroomTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, [CanBeNull] string loadPreset) {
            _loadPreset = loadPreset;

            DataContext = new ViewModel(renderer, car, skinId);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.PreviewSkinCommand, new KeyGesture(Key.PageUp)),
                new InputBinding(Model.NextSkinCommand, new KeyGesture(Key.PageDown)),
                new InputBinding(Model.Car.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
                new InputBinding(Model.OpenSkinDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => Model.Renderer?.Deselect()), new KeyGesture(Key.D, ModifierKeys.Control))
            });
            InitializeComponent();
            Buttons = new Button[0];

            this.OnActualUnload(() => {
                Model.Renderer = null;
                Model.Dispose();
            });
        }

        protected override void OnKeyUp(KeyEventArgs e) {
            if (Model.Mode != Mode.Main && (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack)) {
                Model.Mode = Mode.Main;
                e.Handled = true;
            }

            base.OnKeyUp(e);
        }

        /*protected override void OnKeyDown(KeyEventArgs e) {
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
        }

        public class AmbientShadowParams {
            public double AmbientShadowDiffusion = 60d;
            [JsonProperty("asb")]
            public double AmbientShadowBrightness = 200d;
            public int AmbientShadowIterations = 3200;
            public bool AmbientShadowHideWheels;
            public bool AmbientShadowFade = true;
            [JsonProperty("asa")]
            public bool AmbientShadowAccurate = true;

            [JsonProperty("asbm")]
            public float AmbientShadowBodyMultiplier = 0.75f;

            [JsonProperty("aswm")]
            public float AmbientShadowWheelMultiplier = 0.35f;
        }

        public static AmbientShadowParams LoadAmbientShadowParams() {
            if (!ValuesStorage.Contains(KeySavedData)) {
                return new AmbientShadowParams();
            }

            try {
                return JsonConvert.DeserializeObject<AmbientShadowParams>(ValuesStorage.GetString(KeySavedData));
            } catch (Exception e) {
                Logging.Warning(e);
                return new AmbientShadowParams();
            }
        }

        public bool CanSelectNodes => Model.CanSelectNodes();

        public class ViewModel : NotifyPropertyChanged, IDisposable {
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
                        } else {
                            LoadSkinItems();
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
                var darkRenderer = Renderer as DarkKn5ObjectRenderer;
                if (Settings != null && darkRenderer != null) {
                    CmPreviewsSettings.Transfer(Settings, darkRenderer);
                }
            }));

            private ToolsKn5ObjectRenderer _renderer;

            [CanBeNull]
            public ToolsKn5ObjectRenderer Renderer {
                get => _renderer;
                set {
                    if (Equals(value, _renderer)) return;
                    _renderer = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DarkRenderer));

                    var dark = value as DarkKn5ObjectRenderer;
                    Settings = dark != null ? new DarkRendererSettings(dark) : null;
                    Cars = dark != null ? new DarkRendererCars(dark) : null;
                }
            }

            [CanBeNull]
            public DarkKn5ObjectRenderer DarkRenderer => _renderer as DarkKn5ObjectRenderer;

            private DarkRendererSettings _settings;

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

            private class SaveableData : AmbientShadowParams {
                public bool LiveReload;

                [JsonProperty("cp")]
                public double[] CameraPosition = { 3.194, 0.342, 13.049 };

                [JsonProperty("cl")]
                public double[] CameraLookAt = { 0, 0, 0 };

                [JsonProperty("cf")]
                public float CameraFov = 36f;

                [JsonProperty("co")]
                public bool CameraOrbit = true;

                [JsonProperty("cr")]
                public bool CameraAutoRotate = true;

                [JsonProperty("cg")]
                public bool CameraAutoAdjustTarget = true;
            }

            protected ISaveHelper Saveable { set; get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            public ViewModel([NotNull] ToolsKn5ObjectRenderer renderer, CarObject carObject, string skinId) {
                Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
                Renderer.PropertyChanged += OnRendererPropertyChanged;
                Renderer.CameraMoved += OnCameraMoved;
                OnCarNodeUpdated();

                CameraLookAt.PropertyChanged += OnCameraCoordinatesChanged;
                CameraPosition.PropertyChanged += OnCameraCoordinatesChanged;

                Car = carObject;
                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>(KeySavedData, () => new SaveableData {
                    AmbientShadowDiffusion = AmbientShadowDiffusion,
                    AmbientShadowBrightness = AmbientShadowBrightness,
                    AmbientShadowIterations = AmbientShadowIterations,
                    AmbientShadowHideWheels = AmbientShadowHideWheels,
                    AmbientShadowFade = AmbientShadowFade,
                    AmbientShadowAccurate = AmbientShadowAccurate,
                    AmbientShadowBodyMultiplier = AmbientShadowBodyMultiplier,
                    AmbientShadowWheelMultiplier = AmbientShadowWheelMultiplier,
                    LiveReload = renderer.MagickOverride,

                    CameraPosition = CameraPosition.ToArray(),
                    CameraLookAt = CameraLookAt.ToArray(),
                    CameraFov = CameraFov,
                    CameraOrbit = CameraOrbit,
                    CameraAutoRotate = CameraAutoRotate,
                    CameraAutoAdjustTarget = CameraAutoAdjustTarget,
                }, Load);

                Saveable.Initialize();
            }

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

                    if (!_cameraBusy && !_cameraIgnoreNext && Renderer != null) {
                        Renderer.AutoRotate = value;
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

                    if (!_cameraBusy && !_cameraIgnoreNext && Renderer != null) {
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

            private bool _cameraBusy, _cameraIgnoreNext;

            private void OnCameraMoved(object sender, EventArgs e) {
                if (_cameraIgnoreNext) {
                    _cameraIgnoreNext = false;
                } else {
                    SyncCamera();
                }
            }

            private void SyncCamera() {
                var renderer = Renderer;
                if (renderer == null || _cameraBusy) return;
                _cameraBusy = true;

                try {
                    CameraPosition.Set(renderer.Camera.Position);
                    CameraLookAt.Set(renderer.CameraOrbit?.Target ?? renderer.Camera.Position + renderer.Camera.Look);
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
                        renderer.SetCameraOrbit(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians());
                    } else {
                        renderer.SetCamera(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians());
                    }

                    renderer.AutoRotate = CameraAutoRotate;
                    renderer.AutoAdjustTarget = CameraAutoAdjustTarget;
                    _cameraIgnoreNext = true;
                } finally {
                    _cameraBusy = false;
                }
            }
            #endregion

            private void Load(SaveableData o) {
                AmbientShadowDiffusion = o.AmbientShadowDiffusion;
                AmbientShadowBrightness = o.AmbientShadowBrightness;
                AmbientShadowIterations = o.AmbientShadowIterations;
                AmbientShadowHideWheels = o.AmbientShadowHideWheels;
                AmbientShadowFade = o.AmbientShadowFade;
                AmbientShadowAccurate = o.AmbientShadowAccurate;
                AmbientShadowBodyMultiplier = o.AmbientShadowBodyMultiplier;
                AmbientShadowWheelMultiplier = o.AmbientShadowWheelMultiplier;

                if (Renderer != null) {
                    Renderer.MagickOverride = o.LiveReload;
                }

                _cameraBusy = true;
                try {
                    CameraPosition.Set(o.CameraPosition);
                    CameraLookAt.Set(o.CameraLookAt);
                    CameraFov = o.CameraFov;
                    CameraOrbit = o.CameraOrbit;
                    CameraAutoRotate = o.CameraAutoRotate;
                    CameraAutoAdjustTarget = o.CameraAutoAdjustTarget;
                } finally {
                    _cameraBusy = false;
                    UpdateCamera();
                }
            }

            private void Reset(bool saveLater) {
                Load(new SaveableData());
                if (saveLater) {
                    SaveLater();
                }
            }

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

                    case nameof(Renderer.AmbientShadowSizeChanged):
                        ActionExtension.InvokeInMainThread(() => {
                            _ambientShadowSizeSaveCommand?.RaiseCanExecuteChanged();
                            _ambientShadowSizeResetCommand?.RaiseCanExecuteChanged();
                        });
                        break;
                }
            }

            private DelegateCommand _mainModeCommand;

            public DelegateCommand MainModeCommand => _mainModeCommand ?? (_mainModeCommand = new DelegateCommand(() => {
                Mode = Mode.Main;
            }));

            #region Skin
            private void DisposeSkinItems() {
                FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update -= OnLicensePlatesChanged;
                SkinItems = null;
            }

            private void LoadSkinItems() {
                var skinItems = PaintShop.GetPaintableItems(Car.Id, Renderer?.MainSlot.Kn5).ToList();
                SkinItems = skinItems;
                UpdateLicensePlatesStyles();
                FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update += OnLicensePlatesChanged;

                try {
                    var skin = Path.Combine(Skin.Location, "cm_skin.json");
                    if (File.Exists(skin)) {
                        var jObj = JObject.Parse(File.ReadAllText(skin));
                        foreach (var pair in jObj) {
                            if (pair.Value.Type != JTokenType.Object) continue;
                            skinItems.FirstOrDefault(x => PaintShop.NameToId(x.DisplayName, false) == pair.Key)?
                                     .Deserialize((JObject)pair.Value);
                        }
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private IList<PaintShop.PaintableItem> _skinItems;

            [CanBeNull]
            public IList<PaintShop.PaintableItem> SkinItems {
                get => _skinItems;
                set {
                    if (Equals(value, _skinItems)) return;

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.PropertyChanged -= OnSkinItemPropertyChanged;
                            item.Dispose();
                            item.SetRenderer(null);
                        }
                    }

                    _skinItems = value;
                    OnPropertyChanged();
                    _skinSaveChangesCommand?.RaiseCanExecuteChanged();
                    _skinSaveAsNewCommand?.RaiseCanExecuteChanged();

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.SetRenderer(Renderer);
                            item.PropertyChanged += OnSkinItemPropertyChanged;
                        }
                    }
                }
            }

            private void OnSkinItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName != nameof(PaintShop.PaintableItem.Enabled)) return;

                var item = (PaintShop.PaintableItem)sender;
                if (!item.Enabled) return;

                foreach (var next in _skinItems) {
                    if (!ReferenceEquals(next, item) && next.Enabled && item.AffectedTextures.Any(next.AffectedTextures.Contains)) {
                        next.Enabled = false;
                    }
                }
            }

            private DelegateCommand _toggleSkinModeCommand;

            public DelegateCommand ToggleSkinModeCommand => _toggleSkinModeCommand ?? (_toggleSkinModeCommand = new DelegateCommand(() => {
                Mode = Mode == Mode.Skin ? Mode.Main : Mode.Skin;
            }));

            private async Task SkinSave([NotNull] CarSkinObject skin, bool showWaitingDialog = true) {
                try {
                    var skinsItems = SkinItems;
                    if (Renderer == null || skinsItems == null) return;

                    if (Directory.GetFiles(skin.Location, "*.dds").Any() &&
                            ModernDialog.ShowMessage("Original files if exist will be moved to the Recycle Bin. Are you sure?", "Save Changes",
                                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

                    using (showWaitingDialog ? WaitingDialog.Create("Saving…") : null) {
                        var jObj = new JObject();
                        foreach (var item in skinsItems.ToList()) {
                            try {
                                jObj[PaintShop.NameToId(item.DisplayName, false)] = item.Serialize();
                                await item.SaveAsync(skin.Location);
                            } catch (NotImplementedException) {}
                        }

                        var carPaint = skinsItems.OfType<PaintShop.CarPaint>().FirstOrDefault(x => x.Enabled);
                        if (carPaint != null && LiveryGenerator != null) {
                            var liveryStyle = (carPaint.PatternEnabled ? carPaint.CurrentPattern?.LiveryStyle : null) ?? carPaint.LiveryStyle;
                            if (liveryStyle != null) {
                                var colors = new Dictionary<int, Color>(3);

                                foreach (var item in skinsItems.Where(x => x.Enabled).OrderBy(x => x.LiveryPriority)) {
                                    foreach (var pair in item.LiveryColors) {
                                        colors[pair.Key] = pair.Value;
                                    }
                                }

                                if (carPaint.LiveryColorId.HasValue) {
                                    colors[carPaint.LiveryColorId.Value] = carPaint.Color;
                                }

                                var patternColors = carPaint.CurrentPattern?.LiveryColors;
                                if (patternColors != null) {
                                    foreach (var pair in patternColors) {
                                        colors[pair.Key] = pair.Value;
                                    }
                                }

                                Logging.Debug("Livery colors: " + colors.Select(x => $"[{x.Key}={x.Value.ToHexString()}]").JoinToReadableString());

                                var colorsArray = Enumerable.Range(0, 3).Select(x => colors.GetValueOr(x, Colors.White)).ToArray();
                                await LiveryGenerator.CreateLiveryAsync(skin, colorsArray, liveryStyle);
                            }
                        }

                        File.WriteAllText(Path.Combine(skin.Location, "cm_skin.json"), jObj.ToString(Formatting.Indented));
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save skin", e);
                }
            }

            private AsyncCommand _skinSaveChangesCommand;

            public AsyncCommand SkinSaveChangesCommand => _skinSaveChangesCommand ?? (_skinSaveChangesCommand =
                    new AsyncCommand(() => SkinSave(Skin), () => SkinItems?.Any() == true));

            private AsyncCommand _skinSaveAsNewCommand;

            public AsyncCommand SkinSaveAsNewCommand => _skinSaveAsNewCommand ?? (_skinSaveAsNewCommand = new AsyncCommand(async () => {
                var newId = "generated";
                var location = Car.SkinsDirectory;
                var i = 100;
                for (; i > 0; i--) {
                    var defaultId = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(location, newId)));
                    newId = Prompt.Show("Please, enter an ID for a new skin:", "Save As New Skin", defaultId, "?", required: true, maxLength: 120);

                    if (newId == null) return;
                    if (Regex.IsMatch(newId, @"[^\.\w-]")) {
                        ModernDialog.ShowMessage(
                                "ID shouldn’t contain spaces and other weird symbols. I mean, it might, but then original launcher, for example, won’t work with it.",
                                "Invalid ID", MessageBoxButton.OK);
                        continue;
                    }

                    if (FileUtils.Exists(Path.Combine(location, newId))) {
                        ModernDialog.ShowMessage(
                                "Skin with this ID already exists.",
                                "Place Is Taken", MessageBoxButton.OK);
                        continue;
                    }

                    break;
                }

                if (i == 0) return;

                using (var waiting = WaitingDialog.Create("Saving…")) {
                    Directory.CreateDirectory(Path.Combine(location, newId));

                    CarSkinObject skin = null;
                    for (var j = 0; j < 5; j++) {
                        await Task.Delay(500);
                        skin = Car.GetSkinById(newId);
                        if (skin != null) {
                            break;
                        }
                    }

                    if (skin == null) {
                        waiting.Dispose();
                        NonfatalError.Notify("Can’t save skin", "Skin can’t be created?");
                        return;
                    }

                    await SkinSave(skin, false);
                    Skin = skin;
                }
            }, () => SkinItems?.Any() == true));

            private void OnLicensePlatesChanged(object sender, EventArgs e) {
                UpdateLicensePlatesStyles();
            }

            private void UpdateLicensePlatesStyles() {
                var skinsItems = SkinItems;
                if (Renderer == null || skinsItems == null) return;

                var styles = FilesStorage.Instance.GetContentDirectories(ContentCategory.LicensePlates).ToList();
                foreach (var item in skinsItems.OfType<PaintShop.LicensePlate>()) {
                    item.SetStyles(styles);
                }
            }
            #endregion

            private DelegateCommand _copyCameraPositionCommand;

            public DelegateCommand CopyCameraPositionCommand => _copyCameraPositionCommand ?? (_copyCameraPositionCommand = new DelegateCommand(() => {
                var renderer = Renderer;
                if (renderer == null) return;
                ShowMessage(string.Format("Camera position: {0}\nLook at: {1}\nFOV: {2:F1}°",
                        ToString(renderer.Camera.Position), ToString(renderer.Camera.Position + renderer.Camera.Look),
                        180d / Math.PI * renderer.Camera.FovY),
                        "Camera Position");
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

            #region Ambient Shadows
            private DelegateCommand _toggleAmbientShadowModeCommand;

            public DelegateCommand ToggleAmbientShadowModeCommand => _toggleAmbientShadowModeCommand ?? (_toggleAmbientShadowModeCommand = new DelegateCommand(() => {
                Mode = Mode == Mode.AmbientShadows ? Mode.Main : Mode.AmbientShadows;
            }));

            private static string ToString(Vector3 vec) {
                return $"{-vec.X:F3}, {vec.Y:F3}, {vec.Z:F3}";
            }

            private double _ambientShadowDiffusion;

            public double AmbientShadowDiffusion {
                get => _ambientShadowDiffusion;
                set {
                    value = value.Clamp(0.0, 100.0);
                    if (Equals(value, _ambientShadowDiffusion)) return;
                    _ambientShadowDiffusion = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _ambientShadowBrightness;

            public double AmbientShadowBrightness {
                get => _ambientShadowBrightness;
                set {
                    value = value.Clamp(150.0, 800.0);
                    if (Equals(value, _ambientShadowBrightness)) return;
                    _ambientShadowBrightness = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int _ambientShadowIterations;

            public int AmbientShadowIterations {
                get => _ambientShadowIterations;
                set {
                    value = value.Round(100).Clamp(400, 24000);
                    if (Equals(value, _ambientShadowIterations)) return;
                    _ambientShadowIterations = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ambientShadowHideWheels;

            public bool AmbientShadowHideWheels {
                get => _ambientShadowHideWheels;
                set {
                    if (Equals(value, _ambientShadowHideWheels)) return;
                    _ambientShadowHideWheels = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ambientShadowFade;

            public bool AmbientShadowFade {
                get => _ambientShadowFade;
                set {
                    if (Equals(value, _ambientShadowFade)) return;
                    _ambientShadowFade = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ambientShadowAccurate;

            public bool AmbientShadowAccurate {
                get => _ambientShadowAccurate;
                set {
                    if (Equals(value, _ambientShadowAccurate)) return;
                    _ambientShadowAccurate = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private float _ambientShadowBodyMultiplier = 0.8f;

            public float AmbientShadowBodyMultiplier {
                get => _ambientShadowBodyMultiplier;
                set {
                    if (Equals(value, _ambientShadowBodyMultiplier)) return;
                    _ambientShadowBodyMultiplier = value;
                    OnPropertyChanged();
                }
            }

            private float _ambientShadowWheelMultiplier = 0.69f;

            public float AmbientShadowWheelMultiplier {
                get => _ambientShadowWheelMultiplier;
                set {
                    if (Equals(value, _ambientShadowWheelMultiplier)) return;
                    _ambientShadowWheelMultiplier = value;
                    OnPropertyChanged();
                }
            }

            private AsyncCommand _updateAmbientShadowCommand;

            public AsyncCommand UpdateAmbientShadowCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async () => {
                if (Renderer?.AmbientShadowSizeChanged == true) {
                    AmbientShadowSizeSaveCommand.Execute();
                    if (Renderer?.AmbientShadowSizeChanged == true) return;
                }

                try {
                    using (var waiting = WaitingDialog.Create(ControlsStrings.CustomShowroom_AmbientShadows_Updating)) {
                        var cancellation = waiting.CancellationToken;
                        var progress = (IProgress<double>)waiting;

                        await Task.Run(() => {
                            var kn5 = Renderer?.MainSlot.Kn5;
                            if (kn5 == null) return;

                            using (var renderer = new AmbientShadowRenderer(kn5, Car.AcdData) {
                                DiffusionLevel = (float)AmbientShadowDiffusion / 100f,
                                SkyBrightnessLevel = (float)AmbientShadowBrightness / 100f,
                                Iterations = AmbientShadowIterations,
                                HideWheels = AmbientShadowHideWheels,
                                Fade = AmbientShadowFade,
                                CorrectLighting = AmbientShadowAccurate,
                                BodyMultipler = AmbientShadowBodyMultiplier,
                                WheelMultipler = AmbientShadowWheelMultiplier,
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

            private DelegateCommand _ambientShadowSizeSaveCommand;

            public DelegateCommand AmbientShadowSizeSaveCommand => _ambientShadowSizeSaveCommand ?? (_ambientShadowSizeSaveCommand = new DelegateCommand(() => {
                if (Renderer == null || File.Exists(Path.Combine(Car.Location, "data.acd")) && ModernDialog.ShowMessage(
                        ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedDataMessage,
                        ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedData, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

                var data = Path.Combine(Car.Location, "data");
                Directory.CreateDirectory(data);
                new IniFile {
                    ["SETTINGS"] = {
                        ["WIDTH"] = Renderer.AmbientShadowWidth,
                        ["LENGTH"] = Renderer.AmbientShadowLength,
                    }
                }.Save(Path.Combine(data, "ambient_shadows.ini"));

                Renderer.AmbientShadowSizeChanged = false;
            }, () => Renderer?.AmbientShadowSizeChanged == true));

            private DelegateCommand _ambientShadowResetCommand;

            public DelegateCommand AmbientShadowResetCommand => _ambientShadowResetCommand ?? (_ambientShadowResetCommand = new DelegateCommand(() => {
                Reset(true);
            }));

            private DelegateCommand _ambientShadowSizeFitCommand;

            public DelegateCommand AmbientShadowSizeFitCommand => _ambientShadowSizeFitCommand ?? (_ambientShadowSizeFitCommand = new DelegateCommand(() => {
                Renderer?.FitAmbientShadowSize();
            }, () => Renderer != null));

            private DelegateCommand _ambientShadowSizeResetCommand;

            public DelegateCommand AmbientShadowSizeResetCommand => _ambientShadowSizeResetCommand ?? (_ambientShadowSizeResetCommand = new DelegateCommand(() => {
                Renderer?.ResetAmbientShadowSize();
            }, () => Renderer?.AmbientShadowSizeChanged == true));
            #endregion

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
                    using (var waiting = new WaitingDialog(Tools.ToolsStrings.Common_Exporting)) {
                        await Task.Delay(1);

                        if (FileUtils.Exists(destination) &&
                                MessageBox.Show(string.Format(ControlsStrings.Common_FolderExists, @"unpacked"), Tools.ToolsStrings.Common_Destination,
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
                    NonfatalError.Notify(string.Format(Tools.ToolsStrings.Common_CannotUnpack, Tools.ToolsStrings.Common_KN5), e);
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

            private DelegateCommand _viewObjectCommand;

            public DelegateCommand ViewObjectCommand => _viewObjectCommand ?? (_viewObjectCommand = new DelegateCommand(() => {
                var obj = Renderer?.SelectedObject?.OriginalNode;
                if (obj == null) return;

                ShowMessage(string.Format(ControlsStrings.CustomShowroom_ObjectInformation, obj.Name, obj.NodeClass.GetDescription(), obj.Active,
                        obj.IsRenderable, obj.IsVisible, obj.IsTransparent, obj.CastShadows, obj.Layer,
                        obj.LodIn, obj.LodOut, Renderer.SelectedMaterial?.Name ?? @"?"), obj.Name);
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
                DisposeSkinItems();
            }
        }
    }
}
