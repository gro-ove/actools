using System;
using System.Collections;
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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;
using OxyPlot;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = System.Windows.Controls.Control;
using FileDialogCustomPlace = Microsoft.Win32.FileDialogCustomPlace;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.CustomShowroom {
    public partial class LiteShowroomTools {
        private const string KeySavedData = "__LiteShowroomTools";

        public static ILiveryGenerator LiveryGenerator { get; set; }

        private readonly string _loadPreset;
        private readonly bool _verbose;

        public LiteShowroomTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, [CanBeNull] string loadPreset, ICustomShowroomShots shots,
                bool verbose = false, IEnumerable<CustomShowroomLodDefinition> lodDefinitions = null) {
            _loadPreset = loadPreset;
            _verbose = verbose;

            if (_verbose) {
                Logging.Here();
            }

            DataContext = new ViewModel(renderer, car, skinId, shots, lodDefinitions);
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

            if (_verbose) {
                Logging.Debug("Window created");
            }
        }

        protected override void OnKeyUp(KeyEventArgs e) {
            if (!(Keyboard.FocusedElement is TextBoxBase) && !(Keyboard.FocusedElement is CheckBox) &&
                    Model.Mode != Mode.Main && (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack)) {
                Model.Mode = Mode.Main;
                e.Handled = true;
            }

            base.OnKeyUp(e);
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_verbose) {
                Logging.Debug($"Window loaded: {ActualWidth}×{ActualHeight}, top: {Top}, left: {Left}");
            }

            if (_loaded) return;
            _loaded = true;

            var saveable = Model.Settings;
            if (saveable != null) {
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

            SetSelected();
            Model.PropertyChanged += (s, _) => {
                if (_.PropertyName == nameof(Model.Mode)) {
                    SetSelected();
                }
            };
        }

        private ScaleTransform _selectionScaleTransform;
        private TranslateTransform _selectionTranslateTransform;
        private EasingFunctionBase _selectionEasingFunction;

        [NotNull]
        private Tuple<Point, Size, double> GetSelected() {
            var selected = TabsButtonsPanel.FindLogicalChildren<ModernToggleButton>().FirstOrDefault(x => x.CommandParameter as Mode? == Model.Mode)
                    ?? HomeButton;
            return Tuple.Create(selected.TransformToAncestor(TabsButtonsPanel).Transform(new Point(0, 0)),
                    new Size(selected.ActualWidth / TabsButtonsPanel.ActualWidth, selected.ActualHeight / TabsButtonsPanel.ActualHeight),
                    ReferenceEquals(selected, HomeButton) ? 0d : 1d);
        }

        private void SetSelected() {
            var selected = GetSelected();
            if (_selectionScaleTransform == null) {
                _selectionScaleTransform = new ScaleTransform { ScaleX = selected.Item2.Width, ScaleY = selected.Item2.Height };
                _selectionTranslateTransform = new TranslateTransform { X = selected.Item1.X, Y = selected.Item1.Y };
                CurrentTabHighlight.RenderTransform = new TransformGroup { Children = { _selectionScaleTransform, _selectionTranslateTransform } };
                CurrentTabHighlight.Opacity = selected.Item3;
            } else {
                var duration = TimeSpan.FromSeconds(0.12 + (((_selectionTranslateTransform.X - selected.Item1.X).Abs() - 100d) / 300d).Clamp(0d, 0.3d));
                var easing = _selectionEasingFunction ?? (_selectionEasingFunction = (EasingFunctionBase)FindResource("StandardEase"));
                _selectionTranslateTransform.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = selected.Item1.X, Duration = duration, EasingFunction = easing });
                _selectionTranslateTransform.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation { To = selected.Item1.Y, Duration = duration, EasingFunction = easing });
                _selectionScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation { To = selected.Item2.Width, Duration = duration, EasingFunction = easing });
                _selectionScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation { To = selected.Item2.Height, Duration = duration, EasingFunction = easing });
                CurrentTabHighlight.BeginAnimation(OpacityProperty,
                        new DoubleAnimation { To = selected.Item3, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = easing });
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public enum Mode {
            Main,
            GeneratedLods,
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

                    if (value != Mode.Skin && _mode == Mode.Skin) {
                        DisposeSkinItems();
                    }

                    if (value == Mode.Skin && SkinItems == null && !PluginsManager.Instance.IsPluginEnabled(KnownPlugins.Magick)) {
                        NonfatalError.Notify("Can’t edit skins without Magick.NET plugin", "Please, go to Settings/Plugins and install it first.");
                        value = Mode.Main;
                    }

                    _mode = value;
                    _mainModeCommand?.RaiseCanExecuteChanged();

                    if (renderer?.SelectedObject != null && !IsSelectingMode(value)) {
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

            private bool IsSelectingMode(Mode mode) {
                return mode == Mode.Selected || mode == Mode.GeneratedLods;
            }

            public bool CanSelectNodes() => Mode == Mode.Main || IsSelectingMode(Mode) ||
                    (Renderer as DarkKn5ObjectRenderer)?.Lights.Any(x => x.Tag.IsCarTag && x.AttachedToSelect) == true;

            private DelegateCommand<Mode> _selectModeCommand;

            public DelegateCommand<Mode> SelectModeCommand => _selectModeCommand ?? (_selectModeCommand = new DelegateCommand<Mode>(m => { Mode = m; }));

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
                private set => Apply(value, ref _ambientShadow);
            }

            // Not always renderer is the Dark one
            [CanBeNull]
            public DarkKn5ObjectRenderer DarkRenderer => _renderer as DarkKn5ObjectRenderer;

            private DarkRendererSettings _settings;

            // Set to NULL if renderer is not the Dark one
            [CanBeNull]
            public DarkRendererSettings Settings {
                get => _settings;
                private set => Apply(value, ref _settings);
            }

            private DarkRendererCars _cars;

            public DarkRendererCars Cars {
                get => _cars;
                set => Apply(value, ref _cars);
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

            public bool MagickNetEnabled => PluginsManager.Instance.IsPluginEnabled(KnownPlugins.Magick);

            [NotNull]
            public CarObject Car { get; }

            public IList<CustomShowroomLodDefinition> LodDefinitions { get; }

            public BetterListCollectionView LodDefinitionsView { get; }

            private CustomShowroomLodDefinition _selectedLodDefinition;

            public CustomShowroomLodDefinition SelectedLodDefinition {
                get => _selectedLodDefinition;
                set => Apply(value, ref _selectedLodDefinition, () => {
                    if (value?.Filename != null) {
                        Renderer?.CarNode?.SetCustomLod(value.Filename, value.UseCockpitLrByDefault);
                        UpdateSelectedLodStats();
                    }
                });
            }

            private PlotModel _selectedLodStats;

            public PlotModel SelectedLodStats {
                get => _selectedLodStats;
                set => Apply(value, ref _selectedLodStats);
            }

            private ICommand _viewLodDetailsCommand;

            public ICommand ViewLodDetailsCommand {
                get => _viewLodDetailsCommand;
                set => Apply(value, ref _viewLodDetailsCommand);
            }

            private readonly Busy _updateLodStatsBusy = new Busy();

            private void UpdateSelectedLodStats() {
                _updateLodStatsBusy.DoDelay(() => {
                    SelectedLodStats = SelectedLodDefinition?.StatsFactory?.Invoke(Renderer?.CarNode?.GetCurrentLodKn5());
                    ViewLodDetailsCommand = SelectedLodDefinition?.ViewDetailsFactory?.Invoke(Renderer?.CarNode?.GetCurrentLodKn5());
                }, 100);
            }

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

                [JsonProperty("cp")]
                public double[] CameraPosition = { 3.194, 0.342, 13.049 };

                [JsonProperty("cl")]
                public double[] CameraLookAt = { 0, 0, 0 };

                [JsonProperty("ti")]
                public float CameraTilt;

                [JsonProperty("cf")]
                public float CameraFov = 36f;

                [JsonProperty("co")]
                public bool CameraOrbit = true;

                [JsonProperty("cr")]
                public bool CameraAutoRotate;

                [JsonProperty("cd")]
                public double CameraAutoRotateSpeed = 1d;

                [JsonProperty("cg")]
                public bool CameraAutoAdjustTarget = true;

                [JsonProperty("sw")]
                public int ShotWidth = 1920;

                [JsonProperty("sh")]
                public int ShotHeight = 1080;

                [JsonProperty("sm")]
                public int ShotSizeMultiplier = 1;

                [JsonProperty("sf")]
                public int ShotFormat = (int)RendererShotFormat.Jpeg;

                [JsonProperty("sd")]
                public bool ShotDownsizeInTwo = true;
            }

            protected ISaveHelper Saveable { get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            private readonly ICustomShowroomShots _shots;

            public ViewModel([NotNull] ToolsKn5ObjectRenderer renderer, [NotNull] CarObject carObject, [CanBeNull] string skinId, ICustomShowroomShots shots,
                    IEnumerable<CustomShowroomLodDefinition> lodDefinitions = null) {
                Car = carObject;
                _shots = shots;

                if (lodDefinitions != null) {
                    renderer.DisallowMagickOverride = true;
                    LodDefinitions = lodDefinitions as IList<CustomShowroomLodDefinition> ?? lodDefinitions.ToList();
                    LodDefinitionsView = new BetterListCollectionView((IList)LodDefinitions) {
                        GroupDescriptions = { new PropertyGroupDescription(nameof(CustomShowroomLodDefinition.DisplayLodName)) }
                    };
                    _selectedLodDefinition = LodDefinitions.FirstOrDefault(x => x.Filename == renderer.CarNode?.OriginalFile.OriginalFilename);
                    if (_selectedLodDefinition?.UseCockpitLrByDefault == true && renderer.CarNode?.HasCockpitLr == true) {
                        renderer.CarNode.CockpitLrActive = true;
                    }
                    UpdateSelectedLodStats();
                    renderer.MainSlot.SelectLodPreview += (o, s) => {
                        SelectedLodDefinition = LodDefinitions.ElementAt(
                                ((LodDefinitions.IndexOf(SelectedLodDefinition) + s.SelectOffset) % LodDefinitions.Count + LodDefinitions.Count)
                                        % LodDefinitions.Count);
                        s.Handled = true;
                    };
                }

                Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
                renderer.PropertyChanged += OnRendererPropertyChanged;
                renderer.CameraMoved += OnCameraMoved;
                OnCarNodeUpdated();

                CameraLookAt.PropertyChanged += OnCameraCoordinatesChanged;
                CameraPosition.PropertyChanged += OnCameraCoordinatesChanged;

                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Ignore();

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
                PaintShopSupported = Lazier.CreateAsync(async () => (await CmApiProvider.GetPaintShopIdsAsync())?.ArrayContains(Car.Id) == true);

                var defaultSize = _shots.DefaultSize;
                ShotWidth = defaultSize.Width;
                ShotHeight = defaultSize.Height;

                _shots.PreviewScreenshot += OnScreenshot;
                Car.ChangedFile += OnCarChangedFile;

                if (lodDefinitions != null) {
                    Mode = Mode.GeneratedLods;
                }
            }

            private void OnCarChangedFile(object sender, AcObjectFileChangedArgs args) {
                var paintShopFile = Path.Combine(Car.Location, @"ui", @"cm_paintshop.json");
                var paintShopDirectory = Path.Combine(Car.Location, @"ui", @"cm_paintshop");
                if (FileUtils.IsAffectedBy(paintShopFile, args.Filename)
                        || FileUtils.IsAffectedBy(paintShopDirectory, args.Filename)) {
                    OnPaintShopDataChanged(sender, args);
                }

                var cameraTrajectoryFile = CameraTrajectoryFilename;
                if (FileUtils.IsAffectedBy(cameraTrajectoryFile, args.Filename)) {
                    if (Renderer?.CameraTrajectoryActive == true) {
                        Renderer.SetCameraTrajectory(File.ReadAllText(CameraTrajectoryFilename));
                    }
                    _cameraTrajectoryAvailable = null;
                    OnPropertyChanged(nameof(CameraTrajectoryAvailable));
                }
            }

            private void OnScreenshot(object sender, CancelEventArgs cancelEventArgs) {
                if (Keyboard.Modifiers == ModifierKeys.None) {
                    cancelEventArgs.Cancel = true;
                    ShotCommand.ExecuteAsync(null).Ignore();
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

            public DelegateCommand<string> ShotSetResolutionCommand
                => _shotSetResolutionCommand ?? (_shotSetResolutionCommand = new DelegateCommand<string>(s => {
                    var d = s?.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (d?.Length == 2) {
                        ShotWidth = d[0].As<int>();
                        ShotHeight = d[1].As<int>();
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

            private static readonly StoredValue LastShot = Stored.Get("customShowroom.lastShot");

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
                if (CameraTrajectoryActive || CameraAutoRotate) return;
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
                    if (CameraTrajectoryActive || CameraAutoRotate) return;
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
                    if (CameraTrajectoryActive || CameraAutoRotate) return;
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
                    if (CameraTrajectoryActive || CameraAutoRotate) return;
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

            public string CameraTrajectoryFilename => Path.Combine(Car.Location, @"ui", @"camera_trajectory.json");

            private bool? _cameraTrajectoryAvailable;

            public bool CameraTrajectoryAvailable {
                get => _cameraTrajectoryAvailable ?? (_cameraTrajectoryAvailable = File.Exists(CameraTrajectoryFilename)).Value;
                set => Apply(value, ref _cameraTrajectoryAvailable);
            }

            private DelegateCommand _editTrajectoriesCommand;

            public DelegateCommand EditTrajectoriesCommand => _editTrajectoriesCommand ?? (_editTrajectoriesCommand = new DelegateCommand(() => {
                if (!File.Exists(CameraTrajectoryFilename)) {
                    File.WriteAllBytes(CameraTrajectoryFilename, BinaryResources.ShowroomDefaultTrajectories);
                }
                WindowsHelper.OpenFile(CameraTrajectoryFilename);
            }));

            private bool _cameraTrajectoryActive;

            public bool CameraTrajectoryActive {
                get => _cameraTrajectoryActive;
                set {
                    var newValue = value && CameraTrajectoryAvailable;
                    if (Equals(newValue, _cameraTrajectoryActive)) return;
                    _cameraTrajectoryActive = newValue;

                    if (!_cameraBusy && Renderer != null) {
                        if (newValue) {
                            try {
                                Renderer.SetCameraTrajectory(File.ReadAllText(CameraTrajectoryFilename));
                            } catch (Exception e) {
                                Logging.Error(e);
                            }
                        }
                        Renderer.CameraTrajectoryActive = newValue;
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

            public DelegateCommand ResetCameraCommand => _resetCameraCommand ?? (_resetCameraCommand = new DelegateCommand(() => { Renderer?.ResetCamera(); }));

            private DelegateCommand _loadPresetCameraCommand;

            public DelegateCommand LoadPresetCameraCommand => _loadPresetCameraCommand ?? (_loadPresetCameraCommand = new DelegateCommand(() => {
                try {
                    var showroomPresetsDirectory =
                            PresetsManager.Instance.EnsureDirectory(new PresetsCategory(DarkRendererSettingsValues.DefaultPresetableKeyValue));
                    var previewsPresetsDirectory =
                            PresetsManager.Instance.EnsureDirectory(new PresetsCategory(CmPreviewsSettingsValues.DefaultPresetableKeyValue));

                    var dialog = new OpenFileDialog {
                        InitialDirectory = showroomPresetsDirectory,
                        Filter = string.Format(ToolsStrings.Presets_FileFilter, PresetsCategory.DefaultFileExtension),
                        Title = "Select Custom Showroom or Custom Previews preset",
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
                    renderer.CameraTrajectoryActive = CameraTrajectoryActive;
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

                    if (o.CameraAutoRotate) {
                        ActionExtension.InvokeInMainThreadAsync(() => {
                            if (Renderer != null) Renderer.AutoRotate = true;
                        });
                    }
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
                set => Apply(value, ref _selectedObjectTrianglesCount);
            }

            private string _selectedObjectPath;

            public string SelectedObjectPath {
                get => _selectedObjectPath;
                set => Apply(value, ref _selectedObjectPath);
            }

            private string _selectedObjectTransparent;

            public string SelectedObjectTransparent {
                get => _selectedObjectTransparent;
                set => Apply(value, ref _selectedObjectTransparent);
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
                        CameraAutoRotate = Renderer?.AutoRotate == true;
                        break;

                    case nameof(Renderer.CameraTrajectoryActive):
                        CameraTrajectoryActive = Renderer?.CameraTrajectoryActive != false;
                        break;

                    case nameof(Renderer.AutoRotateSpeed):
                        CameraAutoRotateSpeed = Renderer?.AutoRotateSpeed ?? 1d;
                        break;

                    case nameof(Renderer.SelectedObject):
                        ActionExtension.InvokeInMainThread(() => {
                            if (Renderer?.SelectedObject != null) {
                                if (Mode != Mode.Selected) {
                                    _modeBeforeSelection = Mode;
                                }
                                Mode = Mode.Selected;
                            } else {
                                Mode = _modeBeforeSelection;
                            }
                            SelectedObjectTrianglesCount = Renderer?.SelectedObject?.TrianglesCount ?? 0;

                            var kn5 = Renderer?.GetKn5(Renderer.SelectedObject);
                            var kn5Node = Renderer?.SelectedObject?.OriginalNode;
                            SelectedObjectPath = kn5Node != null ? kn5?.GetParentPath(kn5Node) : null;
                            SelectedObjectTransparent = (kn5Node?.IsTransparent == true ? UiStrings.Yes : UiStrings.No).ToSentenceMember();
                            _viewObjectCommand?.RaiseCanExecuteChanged();
                        });
                        break;

                    case nameof(Renderer.SelectedMaterial):
                        ActionExtension.InvokeInMainThread(() => { _viewMaterialCommand?.RaiseCanExecuteChanged(); });
                        break;
                }
            }

            private Mode _modeBeforeSelection = Mode.Main;

            private DelegateCommand _toggleAmbientShadowModeCommand;

            public DelegateCommand ToggleAmbientShadowModeCommand => _toggleAmbientShadowModeCommand
                    ?? (_toggleAmbientShadowModeCommand = new DelegateCommand(() => Mode = Mode == Mode.AmbientShadows ? Mode.Main : Mode.AmbientShadows));

            private DelegateCommand _mainModeCommand;

            public DelegateCommand MainModeCommand
                => _mainModeCommand ?? (_mainModeCommand = new DelegateCommand(() => { Mode = Mode.Main; }, () => Mode != Mode.Main));

            private AsyncCommand _updateKn5Command;

            public AsyncCommand UpdateKn5Command => _updateKn5Command ?? (_updateKn5Command = new AsyncCommand(async () => {
                var renderer = Renderer;
                var kn5 = renderer?.CarNode?.GetCurrentLodKn5();
                if (kn5 == null) return;

                using (WaitingDialog.Create("Updating model…")) {
                    renderer.CarNode.UpdateCurrentLodKn5Values();
                    await kn5.UpdateKn5(_renderer, _skin);
                }
            }, () => Renderer?.CarNode?.GetCurrentLodKn5()?.IsEditable == true));

            private DelegateCommand _resetDataMovedCommand;

            public DelegateCommand ResetDataMovedCommand => _resetDataMovedCommand ?? (_resetDataMovedCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(
                        "All moved points will be reset to values set in data, and all changes will be lost. Are you sure?",
                        "Reset changes?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Renderer?.CarNode?.ResetMovedDataObjects();
            }));

            private DelegateCommand _updateDataMovedCommand;

            public DelegateCommand UpdateDataMovedCommand => _updateDataMovedCommand ?? (_updateDataMovedCommand = new DelegateCommand(() => {
                if (File.Exists(Path.Combine(Car.Location, "data.acd")) && ModernDialog.ShowMessage(
                        ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedDataMessage.Replace("ambient_shadows.ini", "…"),
                        ControlsStrings.CustomShowroom_AmbientShadowsSize_EncryptedData, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                var data = Path.Combine(Car.Location, "data");
                Directory.CreateDirectory(data);
                Renderer?.CarNode?.SaveMovedDataObjects(data);
            }));

            #region Commands
            private DelegateCommand _nextSkinCommand;

            public DelegateCommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new DelegateCommand(() => { Renderer?.SelectNextSkin(); }));

            private DelegateCommand _previewSkinCommand;

            public DelegateCommand PreviewSkinCommand
                => _previewSkinCommand ?? (_previewSkinCommand = new DelegateCommand(() => { Renderer?.SelectPreviousSkin(); }));

            private DelegateCommand _openSkinDirectoryCommand;

            public DelegateCommand OpenSkinDirectoryCommand
                => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new DelegateCommand(() => { Skin.ViewInExplorer(); }, () => Skin != null));

            private AsyncCommand _unpackKn5Command;

            public AsyncCommand UnpackKn5Command => _unpackKn5Command ?? (_unpackKn5Command = new AsyncCommand(async () => {
                try {
                    var kn5FileName = Renderer?.MainSlot.CarNode?.CurrentLodInformation?.FileName;
                    var kn5 = kn5FileName != null ? await Task.Run(() => Kn5.FromFile(Path.Combine(Car.Location, kn5FileName))) : Renderer?.MainSlot.Kn5;
                    if (kn5 == null) return;

                    Kn5.FbxConverterLocation = PluginsManager.Instance.GetPluginFilename(KnownPlugins.FbxConverter, "FbxConverter.exe");

                    var directoryName = $@"unpacked-{Path.GetFileNameWithoutExtension(kn5.OriginalFilename ?? @"main")}";
                    var destination = Path.Combine(Car.Location, directoryName);
                    using (var waiting = new WaitingDialog(ToolsStrings.Common_Exporting)) {
                        await Task.Delay(1);

                        if (FileUtils.Exists(destination)) {
                            var response = ModernDialog.ShowMessage(string.Format(ControlsStrings.Common_FolderExists, directoryName),
                                    ToolsStrings.Common_Destination, MessageBoxButton.YesNoCancel);
                            if (response == MessageBoxResult.Cancel) return;
                            if (response == MessageBoxResult.No) {
                                var temp = destination;
                                var i = 1;
                                do {
                                    destination = $"{temp}-{i++}";
                                } while (FileUtils.Exists(destination));
                            }
                        }

                        var name = kn5.RootNode.Name.StartsWith(@"FBX: ") ? kn5.RootNode.Name.Substring(5) : @"model.fbx";
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
            }, () => SettingsHolder.Common.DeveloperMode && PluginsManager.Instance.IsPluginEnabled(KnownPlugins.FbxConverter)
                    && Renderer?.MainSlot.Kn5?.IsEditable == true));
            #endregion

            #region Materials & Textures
            private void ShowMessage(string text, string title, Func<ModernDialog, IEnumerable<Control>> buttons = null) {
                var dlg = new ModernDialog {
                    Title = title,
                    Content = new ScrollViewer {
                        Content = new SelectableBbCodeBlock {
                            Text = text
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

                dlg.Buttons = buttons?.Invoke(dlg).Append(dlg.OkButton) ?? new[] { dlg.OkButton };
                AttachedHelper.GetInstance(_renderer)?.Attach(text, dlg);
            }

            private AsyncCommand _viewObjectCommand;

            public AsyncCommand ViewObjectCommand => _viewObjectCommand ?? (_viewObjectCommand = new AsyncCommand(async () => {
                var obj = Renderer?.SelectedObject;
                if (obj == null) return;

                var attached = AttachedHelper.GetInstance(_renderer);
                if (attached == null) return;

                var kn5 = Renderer?.GetKn5(Renderer.SelectedObject);
                if (kn5 == null) return;

                try {
                    await attached.AttachAndWaitAsync("Kn5ObjectDialog",
                            new Kn5ObjectDialog(Renderer, Car, Skin, kn5, obj));
                } catch (Exception e) {
                    NonfatalError.Notify("Unexpected exception", e);
                }
            }, () => Renderer?.SelectedObject != null && Renderer?.GetKn5(Renderer.SelectedObject)?.IsEditable == true));

            private static string MaterialPropertyToString(Kn5Material.ShaderProperty property) {
                if (property.ValueD != Vec4.Zero) return $"({property.ValueD.X}, {property.ValueD.Y}, {property.ValueD.Z}, {property.ValueD.W})";
                if (property.ValueC != Vec3.Zero) return $"({property.ValueC.X}, {property.ValueC.Y}, {property.ValueC.Z})";
                if (property.ValueB != Vec2.Zero) return $"({property.ValueB.X}, {property.ValueB.Y})";
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

                ShowMessage(sb.ToString(), material.Name, d => new[] {
                    d.CreateCloseDialogButton("Change values", false, false, MessageBoxResult.OK, ChangeMaterialCommand)
                });
            }, () => Renderer?.SelectedMaterial != null && Renderer?.GetKn5(Renderer.SelectedObject)?.IsEditable == true));

            private DelegateCommand _changeMaterialCommand;

            public DelegateCommand ChangeMaterialCommand => _changeMaterialCommand ?? (_changeMaterialCommand = new DelegateCommand(async () => {
                try {
                    var attached = AttachedHelper.GetInstance(_renderer);
                    if (attached == null) return;

                    var kn5 = Renderer?.GetKn5(Renderer.SelectedObject);
                    if (kn5 == null) return;

                    var material = Renderer.SelectedMaterial;
                    if (material == null) return;

                    using (var dialog = new Kn5MaterialDialog(Renderer, Car, Skin, kn5,
                            Renderer.SelectedObject?.OriginalNode.MaterialId ?? uint.MaxValue)) {
                        await attached.AttachAndWaitAsync("Kn5MaterialDialog", dialog);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Unexpected exception", e);
                }
            }, () => Renderer?.GetKn5(Renderer.SelectedObject)?.IsEditable == true));

            private AsyncCommand _ToggleMeshTransparencyCommand;

            public AsyncCommand ToggleMeshTransparencyCommand
                => _ToggleMeshTransparencyCommand ?? (_ToggleMeshTransparencyCommand = new AsyncCommand(async () => {
                    try {
                        var selected = Renderer?.SelectedObject;
                        if (selected == null) return;

                        var kn5 = Renderer?.GetKn5(Renderer.SelectedObject);
                        if (kn5 == null) return;

                        selected.OriginalNode.IsTransparent = !selected.OriginalNode.IsTransparent;
                        selected.SetTransparent(selected.OriginalNode.IsTransparent);
                        using (WaitingDialog.Create("Updating model…")) {
                            await kn5.UpdateKn5(_renderer, _skin);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Unexpected exception", e);
                    }
                }, () => Renderer?.GetKn5(Renderer.SelectedObject)?.IsEditable == true));

            private DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation> _viewTextureCommand;

            public DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation> ViewTextureCommand
                => _viewTextureCommand ?? (_viewTextureCommand = new DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation>(async o => {
                    var attached = AttachedHelper.GetInstance(_renderer);
                    if (attached == null) return;

                    var kn5 = Renderer?.GetKn5(Renderer.SelectedObject);
                    if (kn5 == null) return;

                    try {
                        await attached.AttachAndWaitAsync("Kn5TextureDialog",
                                new Kn5TextureDialog(Renderer, Car, Skin, kn5, o.TextureName,
                                        Renderer.SelectedObject?.OriginalNode.MaterialId ?? uint.MaxValue, o.SlotName));
                    } catch (Exception e) {
                        NonfatalError.Notify("Unexpected exception", e);
                    }
                }, o => o != null));
            #endregion

            public void Dispose() {
                Renderer = null;
                Car.ChangedFile -= OnCarChangedFile;
                DisposeSkinItems();
            }
        }

        protected override void CenterOnScreen(WpfScreen screen) {
            Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - ActualHeight) / 2;
            Left = screen.WorkingArea.Right - ActualWidth - 8;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_verbose) {
                Logging.Debug($"Window unloaded: {ActualWidth}×{ActualHeight}, top: {Top}, left: {Left}");
            }
        }
    }
}