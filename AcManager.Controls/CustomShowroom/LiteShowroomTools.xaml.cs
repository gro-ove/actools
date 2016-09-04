using System;
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
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Controls.CustomShowroom {
    public partial class LiteShowroomTools {
        public LiteShowroomTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId) {
            DataContext = new LiteShowroomToolsViewModel(renderer, car, skinId);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.PreviewSkinCommand, new KeyGesture(Key.PageUp)),
                new InputBinding(Model.NextSkinCommand, new KeyGesture(Key.PageDown)),
                new InputBinding(Model.Car.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
                new InputBinding(Model.OpenSkinDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(new RelayCommand(o => Model.Renderer?.Deselect()), new KeyGesture(Key.D, ModifierKeys.Control))
            });
            InitializeComponent();
            Buttons = new Button[0];
        }

        private DispatcherTimer _timer;

        private void LiteShowroomTools_OnLoaded(object sender, RoutedEventArgs e) {
            if (_timer != null) return;
            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(300),
                IsEnabled = true
            };
            _timer.Tick += Timer_Tick;
        }

        private void LiteShowroomTools_OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Renderer = null;

            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void Timer_Tick(object sender, EventArgs e) {
            Model.OnTick();
        }

        public LiteShowroomToolsViewModel Model => (LiteShowroomToolsViewModel)DataContext;

        public class LiteShowroomToolsViewModel : NotifyPropertyChanged {
            private ToolsKn5ObjectRenderer _renderer;

            [CanBeNull]
            public ToolsKn5ObjectRenderer Renderer {
                get { return _renderer; }
                set {
                    if (Equals(value, _renderer)) return;
                    _renderer = value;
                    OnPropertyChanged();
                }
            }

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
                public double AmbientShadowDiffusion, AmbientShadowBrightness;
                public int AmbientShadowIterations;
                public bool AmbientShadowHideWheels;
                public bool? AmbientShadowFade;
                public bool LiveReload;
            }
            protected ISaveHelper Saveable { set; get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            public LiteShowroomToolsViewModel([NotNull] ToolsKn5ObjectRenderer renderer, CarObject carObject, string skinId) {
                if (renderer == null) throw new ArgumentNullException(nameof(renderer));

                Renderer = renderer;
                renderer.PropertyChanged += Renderer_PropertyChanged;
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
                    LiveReload = Renderer.LiveReload,
                }, o => {
                    AmbientShadowDiffusion = o.AmbientShadowDiffusion;
                    AmbientShadowBrightness = o.AmbientShadowBrightness;
                    AmbientShadowIterations = o.AmbientShadowIterations;
                    AmbientShadowHideWheels = o.AmbientShadowHideWheels;
                    AmbientShadowFade = o.AmbientShadowFade ?? true;
                    Renderer.LiveReload = o.LiveReload;
                }, () => {
                    Reset(false);
                });
                Saveable.Initialize();
            }

            private void Reset(bool saveLater) {
                AmbientShadowDiffusion = 60d;
                AmbientShadowBrightness = 230d;
                AmbientShadowIterations = 3200;
                AmbientShadowHideWheels = false;
                AmbientShadowFade = true;

                if (Renderer != null) {
                    Renderer.LiveReload = false;
                }

                if (saveLater) {
                    SaveLater();
                }
            }

            private void Renderer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.LiveReload):
                        SaveLater();
                        break;

                    case nameof(Renderer.CurrentSkin):
                        Skin = Car.GetSkinById(Renderer?.CurrentSkin ?? "");
                        break;

                    case nameof(Renderer.SelectedObject):
                        ViewObjectCommand.OnCanExecuteChanged();
                        break;

                    case nameof(Renderer.SelectedMaterial):
                        ViewMaterialCommand.OnCanExecuteChanged();
                        break;

                    case nameof(Renderer.AmbientShadowSizeChanged):
                        AmbientShadowSizeSaveCommand.OnCanExecuteChanged();
                        AmbientShadowSizeResetCommand.OnCanExecuteChanged();
                        break;
                }
            }

            private void Renderer_Tick(object sender, AcTools.Render.Base.TickEventArgs args) { }

            public void OnTick() { }

            #region Ambient Shadows
            private bool _ambientShadowsMode;

            public bool AmbientShadowsMode {
                get { return _ambientShadowsMode; }
                set {
                    if (Equals(value, _ambientShadowsMode)) return;
                    _ambientShadowsMode = value;
                    OnPropertyChanged();
                }
            }

            private RelayCommand _toggleAmbientShadowModeCommand;

            public RelayCommand ToggleAmbientShadowModeCommand => _toggleAmbientShadowModeCommand ?? (_toggleAmbientShadowModeCommand = new RelayCommand(o => {
                AmbientShadowsMode = !AmbientShadowsMode;
                if (!AmbientShadowsMode && Renderer != null) {
                    Renderer.AmbientShadowHighlight = false;
                }
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
                    value = value.Round(100).Clamp(400, 4000);
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

            private AsyncCommand _updateAmbientShadowCommand;

            public AsyncCommand UpdateAmbientShadowCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async o => {
                if (Renderer?.AmbientShadowSizeChanged == true) {
                    AmbientShadowSizeSaveCommand.Execute(null);

                    if (Renderer?.AmbientShadowSizeChanged == true) return;
                }

                try {
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(ControlsStrings.CustomShowroom_AmbientShadows_Updating);

                        await Task.Run(() => {
                            if (Renderer == null) return;
                            using (var renderer = new AmbientShadowKn5ObjectRenderer(Renderer.Kn5, Car.Location)) {
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

                        foreach (var s in new[] {
                            @"body_shadow.png",
                            @"tyre_0_shadow.png",
                            @"tyre_1_shadow.png",
                            @"tyre_2_shadow.png",
                            @"tyre_3_shadow.png"
                        }) {
                            if (Renderer == null) return;
                            await Renderer.UpdateTextureAsync(Path.Combine(Car.Location, s));
                        }

                        GC.Collect();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
                }
            }));

            private RelayCommand _ambientShadowSizeSaveCommand;

            public RelayCommand AmbientShadowSizeSaveCommand => _ambientShadowSizeSaveCommand ?? (_ambientShadowSizeSaveCommand = new RelayCommand(o => {
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
                }.SaveAs(Path.Combine(data, "ambient_shadows.ini"));

                Renderer.AmbientShadowSizeChanged = false;
            }, o => Renderer?.AmbientShadowSizeChanged == true));

            private RelayCommand _ambientShadowResetCommand;

            public RelayCommand AmbientShadowResetCommand => _ambientShadowResetCommand ?? (_ambientShadowResetCommand = new RelayCommand(o => {
                Reset(true);
            }));

            private RelayCommand _ambientShadowSizeFitCommand;

            public RelayCommand AmbientShadowSizeFitCommand => _ambientShadowSizeFitCommand ?? (_ambientShadowSizeFitCommand = new RelayCommand(o => {
                Renderer?.FitAmbientShadowSize();
            }, o => Renderer != null));

            private RelayCommand _ambientShadowSizeResetCommand;

            public RelayCommand AmbientShadowSizeResetCommand => _ambientShadowSizeResetCommand ?? (_ambientShadowSizeResetCommand = new RelayCommand(o => {
                Renderer?.ResetAmbientShadowSize();
            }, o => Renderer?.AmbientShadowSizeChanged == true));
            #endregion

            #region Commands
            private RelayCommand _nextSkinCommand;

            public RelayCommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new RelayCommand(o => {
                Renderer?.SelectNextSkin();
            }));

            private RelayCommand _previewSkinCommand;

            public RelayCommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new RelayCommand(o => {
                Renderer?.SelectPreviousSkin();
            }));

            private RelayCommand _openSkinDirectoryCommand;

            public RelayCommand OpenSkinDirectoryCommand => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new RelayCommand(o => {
                Skin.ViewInExplorer();
            }, o => Skin != null));

            private AsyncCommand _unpackKn5Command;

            public AsyncCommand UnpackKn5Command => _unpackKn5Command ?? (_unpackKn5Command = new AsyncCommand(async o => {
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
            }, o => SettingsHolder.Common.MsMode && PluginsManager.Instance.IsPluginEnabled("FbxConverter") && Renderer?.Kn5 != null));
            #endregion

            #region Materials & Textures
            private static void ShowMessage(string text, string title) {
                var dlg = new ModernDialog {
                    Title = title,
                    Content = new ScrollViewer {
                        Content = new BbCodeBlock { BbCode = text, Margin = new Thickness(0, 0, 0, 8) },
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

            private RelayCommand _viewObjectCommand;

            public RelayCommand ViewObjectCommand => _viewObjectCommand ?? (_viewObjectCommand = new RelayCommand(o => {
                var obj = Renderer?.SelectedObject.OriginalNode;
                if (obj == null) return;

                ShowMessage(string.Format(ControlsStrings.CustomShowroom_ObjectInformation, obj.Name, obj.NodeClass.GetDescription(), obj.Active,
                        obj.IsRenderable, obj.IsVisible, obj.IsTransparent, obj.CastShadows, obj.Layer,
                        obj.LodIn, obj.LodOut, Renderer.SelectedMaterial?.Name ?? @"?"), obj.Name);
            }, o => Renderer?.SelectedObject != null));

            private static string MaterialPropertyToString(Kn5Material.ShaderProperty property) {
                if (property.ValueD.Any(x => !Equals(x, 0f))) return $"({property.ValueD.JoinToString(@", ")})";
                if (property.ValueC.Any(x => !Equals(x, 0f))) return $"({property.ValueC.JoinToString(@", ")})";
                if (property.ValueB.Any(x => !Equals(x, 0f))) return $"({property.ValueB.JoinToString(@", ")})";
                return property.ValueA.ToInvariantString();
            }

            private RelayCommand _viewMaterialCommand;

            public RelayCommand ViewMaterialCommand => _viewMaterialCommand ?? (_viewMaterialCommand = new RelayCommand(o => {
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
            }, o => Renderer?.SelectedMaterial != null));

            private RelayCommand _viewTextureCommand;

            public RelayCommand ViewTextureCommand => _viewTextureCommand ?? (_viewTextureCommand = new RelayCommand(o => {
                if (Renderer == null) return;
                new CarTextureDialog(Renderer.Kn5, ((ToolsKn5ObjectRenderer.TextureInformation)o).TextureName).ShowDialog();
            }, o => o is ToolsKn5ObjectRenderer.TextureInformation));
            #endregion
        }
    }
}
