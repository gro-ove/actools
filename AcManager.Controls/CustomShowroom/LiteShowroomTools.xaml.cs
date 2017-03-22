using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SlimDX;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Controls.CustomShowroom {
    public partial class LiteShowroomTools {
        public LiteShowroomTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId) {
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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Renderer = null;

            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void Timer_Tick(object sender, EventArgs e) {
            Model.OnTick();
        }

        public ViewModel Model => (ViewModel)DataContext;

        public enum Mode {
            Main,
            VisualSettings,
            Selected,
            AmbientShadows,
            Skin
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private Mode _mode = Mode.Main;

            public Mode Mode {
                get { return _mode; }
                set {
                    if (Equals(value, _mode)) return;

                    if (_mode == Mode.AmbientShadows && Renderer != null) {
                        Renderer.AmbientShadowHighlight = false;
                    }

                    if (value == Mode.Skin && SkinItems == null) {
                        if (!PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId)) {
                            NonfatalError.Notify("Can’t edit skins without Magick.NET plugin", "Please, go to Settings/Plugins and install it first.");
                            value = Mode.Main;
                        }

                        SkinItems = PaintShop.GetPaintableItems(Car.Id, Renderer?.Kn5).ToList();
                        UpdateLicensePlatesStyles();
                        FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update += OnLicensePlatesChanged;
                    }

                    _mode = value;
                    OnPropertyChanged();
                }
            }

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
                get { return _renderer; }
                set {
                    if (Equals(value, _renderer)) return;
                    _renderer = value;
                    OnPropertyChanged();

                    var dark = value as DarkKn5ObjectRenderer;
                    Settings = dark != null ? new DarkRendererSettings(dark) : null;
                }
            }

            private DarkRendererSettings _settings;

            [CanBeNull]
            public DarkRendererSettings Settings {
                get { return _settings; }
                private set {
                    if (Equals(value, _settings)) return;
                    _settings = value;
                    OnPropertyChanged();
                }
            }

            public bool MagickNetEnabled => PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId);

            public CarObject Car { get; }

            private CarSkinObject _skin;

            public CarSkinObject Skin {
                get { return _skin; }
                set {
                    if (Equals(value, _skin)) return;
                    _skin = value;
                    OnPropertyChanged();

                    Renderer?.SelectSkin(value?.Id);
                }
            }

            private class SaveableData {
                public double AmbientShadowDiffusion = 60d;
                [JsonProperty("asb")]
                public double AmbientShadowBrightness = 200d;
                public int AmbientShadowIterations = 3200;
                public bool AmbientShadowHideWheels;
                public bool AmbientShadowFade = true;
                public bool LiveReload;
            }

            protected ISaveHelper Saveable { set; get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            public ViewModel([NotNull] ToolsKn5ObjectRenderer renderer, CarObject carObject, string skinId) {
                if (renderer == null) throw new ArgumentNullException(nameof(renderer));

                Renderer = renderer;
                renderer.PropertyChanged += Renderer_PropertyChanged;
                Renderer_CarNodeUpdated();
                renderer.Tick += Renderer_Tick;

                Car = carObject;
                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>("__LiteShowroomTools", () => new SaveableData {
                    AmbientShadowDiffusion = AmbientShadowDiffusion,
                    AmbientShadowBrightness = AmbientShadowBrightness,
                    AmbientShadowIterations = AmbientShadowIterations,
                    AmbientShadowHideWheels = AmbientShadowHideWheels,
                    AmbientShadowFade = AmbientShadowFade,
                    LiveReload = renderer.MagickOverride,
                }, Load);

                Saveable.Initialize();
            }

            private void Load(SaveableData o) {
                AmbientShadowDiffusion = o.AmbientShadowDiffusion;
                AmbientShadowBrightness = o.AmbientShadowBrightness;
                AmbientShadowIterations = o.AmbientShadowIterations;
                AmbientShadowHideWheels = o.AmbientShadowHideWheels;
                AmbientShadowFade = o.AmbientShadowFade;

                if (Renderer != null) {
                    Renderer.MagickOverride = o.LiveReload;
                }
            }

            private void Reset(bool saveLater) {
                Load(new SaveableData());
                if (saveLater) {
                    SaveLater();
                }
            }

            private INotifyPropertyChanged _carNode;

            private void Renderer_CarNodeUpdated() {
                if (_carNode != null) {
                    _carNode.PropertyChanged -= CarNode_PropertyChanged;
                }
                _carNode = _renderer.CarNode;
                if (_carNode != null) {
                    _carNode.PropertyChanged += CarNode_PropertyChanged;
                }
            }

            private void CarNode_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.CarNode.CurrentSkin):
                        Skin = Car.GetSkinById(Renderer?.CarNode?.CurrentSkin ?? "");
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

                    case nameof(Renderer.SelectedObject):
                        Mode = Renderer?.SelectedObject != null ? Mode.Selected : Mode.Main;
                        _viewObjectCommand?.RaiseCanExecuteChanged();
                        break;

                    case nameof(Renderer.SelectedMaterial):
                        _viewMaterialCommand?.RaiseCanExecuteChanged();
                        break;

                    case nameof(Renderer.AmbientShadowSizeChanged):
                        _ambientShadowSizeSaveCommand?.RaiseCanExecuteChanged();
                        _ambientShadowSizeResetCommand?.RaiseCanExecuteChanged();
                        break;
                }
            }

            private void Renderer_Tick(object sender, AcTools.Render.Base.TickEventArgs args) { }

            public void OnTick() { }

            private DelegateCommand _mainModeCommand;

            public DelegateCommand MainModeCommand => _mainModeCommand ?? (_mainModeCommand = new DelegateCommand(() => {
                Mode = Mode.Main;
            }));

            #region Skin
            private IList<PaintShop.PaintableItem> _skinItems;

            public IList<PaintShop.PaintableItem> SkinItems {
                get { return _skinItems; }
                set {
                    if (Equals(value, _skinItems)) return;

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.SetRenderer(null);
                        }
                    }

                    _skinItems = value;
                    OnPropertyChanged();
                    _skinSaveChangesCommand?.RaiseCanExecuteChanged();

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.SetRenderer(Renderer);
                        }
                    }
                }
            }

            private DelegateCommand _toggleSkinModeCommand;

            public DelegateCommand ToggleSkinModeCommand => _toggleSkinModeCommand ?? (_toggleSkinModeCommand = new DelegateCommand(() => {
                Mode = Mode == Mode.Skin ? Mode.Main : Mode.Skin;
            }));

            private AsyncCommand _skinSaveChangesCommand;

            public AsyncCommand SkinSaveChangesCommand => _skinSaveChangesCommand ?? (_skinSaveChangesCommand = new AsyncCommand(async () => {
                if (Renderer == null) return;

                if (Directory.GetFiles(Skin.Location, "*.dds").Any() &&
                        ModernDialog.ShowMessage("Original files if exist will be moved to the Recycle Bin. Are you sure?", "Save Changes",
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

                foreach (var item in SkinItems.Where(x => x.Enabled)) {
                    try {
                        await item.SaveAsync(Skin.Location);
                    } catch (NotImplementedException) {}
                }
            }, () => SkinItems.Any()));

            private void OnLicensePlatesChanged(object sender, EventArgs e) {
                UpdateLicensePlatesStyles();
            }

            private void UpdateLicensePlatesStyles() {
                var styles = FilesStorage.Instance.GetContentDirectories(ContentCategory.LicensePlates).ToList();
                foreach (var item in SkinItems.OfType<PaintShop.LicensePlate>()) {
                    item.SetStyles(styles);
                }
            }
            #endregion

            #region Ambient Shadows
            private ICommand _toggleAmbientShadowModeCommand;

            public ICommand ToggleAmbientShadowModeCommand => _toggleAmbientShadowModeCommand ?? (_toggleAmbientShadowModeCommand = new DelegateCommand(() => {
                Mode = Mode == Mode.AmbientShadows ? Mode.Main : Mode.AmbientShadows;
            }));

            private static string ToString(Vector3 vec) {
                return $"{-vec.X:F3}, {vec.Y:F3}, {vec.Z:F3}";
            }

            private DelegateCommand _copyCameraPositionCommand;

            public DelegateCommand CopyCameraPositionCommand => _copyCameraPositionCommand ?? (_copyCameraPositionCommand = new DelegateCommand(() => {
                var renderer = Renderer;
                if (renderer == null) return;
                ShowMessage(string.Format("Camera position: {0}\nLook at: {1}\nFOV: {2:F1}°",
                        ToString(renderer.Camera.Position), ToString(renderer.Camera.Position + renderer.Camera.Look),
                        180d / Math.PI * renderer.Camera.FovY),
                        "Camera Position");
            }));

            private double _ambientShadowDiffusion;

            public double AmbientShadowDiffusion {
                get { return _ambientShadowDiffusion; }
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
                get { return _ambientShadowBrightness; }
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
                get { return _ambientShadowIterations; }
                set {
                    value = value.Round(100).Clamp(400, 8000);
                    if (Equals(value, _ambientShadowIterations)) return;
                    _ambientShadowIterations = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ambientShadowHideWheels;

            public bool AmbientShadowHideWheels {
                get { return _ambientShadowHideWheels; }
                set {
                    if (Equals(value, _ambientShadowHideWheels)) return;
                    _ambientShadowHideWheels = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _ambientShadowFade;

            public bool AmbientShadowFade {
                get { return _ambientShadowFade; }
                set {
                    if (Equals(value, _ambientShadowFade)) return;
                    _ambientShadowFade = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private CommandBase _updateAmbientShadowCommand;

            public ICommand UpdateAmbientShadowCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async () => {
                if (Renderer?.AmbientShadowSizeChanged == true) {
                    AmbientShadowSizeSaveCommand.Execute(null);

                    if (Renderer?.AmbientShadowSizeChanged == true) return;
                }

                try {
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(ControlsStrings.CustomShowroom_AmbientShadows_Updating);

                        await Task.Run(() => {
                            if (Renderer == null) return;
                            using (var renderer = new AmbientShadowRenderer(Renderer.Kn5, Car.Location)) {
                                renderer.DiffusionLevel = (float)AmbientShadowDiffusion / 100f;
                                renderer.SkyBrightnessLevel = (float)AmbientShadowBrightness / 100f;
                                renderer.Iterations = AmbientShadowIterations;
                                renderer.HideWheels = AmbientShadowHideWheels;
                                renderer.Fade = AmbientShadowFade;

                                renderer.Initialize();
                                renderer.Shot();
                            }
                        });

                        waiting.Report(ControlsStrings.CustomShowroom_AmbientShadows_Reloading);
                        GC.Collect();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
                }
            }));

            private CommandBase _ambientShadowSizeSaveCommand;

            public ICommand AmbientShadowSizeSaveCommand => _ambientShadowSizeSaveCommand ?? (_ambientShadowSizeSaveCommand = new DelegateCommand(() => {
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

            private ICommand _ambientShadowResetCommand;

            public ICommand AmbientShadowResetCommand => _ambientShadowResetCommand ?? (_ambientShadowResetCommand = new DelegateCommand(() => {
                Reset(true);
            }));

            private ICommand _ambientShadowSizeFitCommand;

            public ICommand AmbientShadowSizeFitCommand => _ambientShadowSizeFitCommand ?? (_ambientShadowSizeFitCommand = new DelegateCommand(() => {
                Renderer?.FitAmbientShadowSize();
            }, () => Renderer != null));

            private CommandBase _ambientShadowSizeResetCommand;

            public ICommand AmbientShadowSizeResetCommand => _ambientShadowSizeResetCommand ?? (_ambientShadowSizeResetCommand = new DelegateCommand(() => {
                Renderer?.ResetAmbientShadowSize();
            }, () => Renderer?.AmbientShadowSizeChanged == true));
            #endregion

            #region Commands
            private ICommand _nextSkinCommand;

            public ICommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new DelegateCommand(() => {
                Renderer?.SelectNextSkin();
            }));

            private ICommand _previewSkinCommand;

            public ICommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new DelegateCommand(() => {
                Renderer?.SelectPreviousSkin();
            }));

            private CommandBase _openSkinDirectoryCommand;

            public ICommand OpenSkinDirectoryCommand => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new DelegateCommand(() => {
                Skin.ViewInExplorer();
            }, () => Skin != null));

            private CommandBase _unpackKn5Command;

            public ICommand UnpackKn5Command => _unpackKn5Command ?? (_unpackKn5Command = new AsyncCommand(async () => {
                if (Renderer?.Kn5 == null) return;

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

                        var name = Renderer.Kn5.RootNode.Name.StartsWith(@"FBX: ") ? Renderer.Kn5.RootNode.Name.Substring(5) :
                                @"model.fbx";
                        Directory.CreateDirectory(destination);
                        await Renderer.Kn5.ExportFbxWithIniAsync(Path.Combine(destination, name), waiting, waiting.CancellationToken);

                        var textures = Path.Combine(destination, "texture");
                        Directory.CreateDirectory(textures);
                        await Renderer.Kn5.ExportTexturesAsync(textures, waiting, waiting.CancellationToken);
                    }

                    Process.Start(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(string.Format(Tools.ToolsStrings.Common_CannotUnpack, Tools.ToolsStrings.Common_KN5), e);
                }
            }, () => SettingsHolder.Common.MsMode && PluginsManager.Instance.IsPluginEnabled("FbxConverter") && Renderer?.Kn5 != null));
            #endregion

            #region Materials & Textures
            private static void ShowMessage(string text, string title) {
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

                dlg.Buttons = new[] { dlg.OkButton };
                dlg.ShowDialog();
            }

            private CommandBase _viewObjectCommand;

            public ICommand ViewObjectCommand => _viewObjectCommand ?? (_viewObjectCommand = new DelegateCommand(() => {
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

            private CommandBase _viewMaterialCommand;

            public ICommand ViewMaterialCommand => _viewMaterialCommand ?? (_viewMaterialCommand = new DelegateCommand(() => {
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

                ShowMessage(sb.ToString(), material.Name);
            }, () => Renderer?.SelectedMaterial != null));

            private CommandBase _viewTextureCommand;

            public ICommand ViewTextureCommand => _viewTextureCommand ?? (_viewTextureCommand = new DelegateCommand<ToolsKn5ObjectRenderer.TextureInformation>(o => {
                if (Renderer?.Kn5 == null) return;
                new CarTextureDialog(Renderer, Skin, Renderer.GetKn5(Renderer.SelectedObject), o.TextureName) {
                    Owner = null
                }.ShowDialog();
            }, o => o != null));
            #endregion

            public void Dispose() {
                SkinItems?.DisposeEverything();
                FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update -= OnLicensePlatesChanged;
            }
        }
    }
}
