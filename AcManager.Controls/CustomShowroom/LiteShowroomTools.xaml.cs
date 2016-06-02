using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

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

        private void Label_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 1) return;

            var label = (Label)sender;
            Keyboard.Focus(label.Target);
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
                    LiveReload = Renderer.LiveReload,
                }, o => {
                    AmbientShadowDiffusion = o.AmbientShadowDiffusion;
                    AmbientShadowBrightness = o.AmbientShadowBrightness;
                    AmbientShadowIterations = o.AmbientShadowIterations;
                    AmbientShadowHideWheels = o.AmbientShadowHideWheels;
                    Renderer.LiveReload = o.LiveReload;
                }, () => {
                    AmbientShadowDiffusion = 40.0;
                    AmbientShadowBrightness = 350.0;
                    AmbientShadowIterations = 2000;
                    AmbientShadowHideWheels = false;
                    Renderer.LiveReload = false;
                });
                Saveable.Initialize();
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
                    value = MathUtils.Clamp(value, 0.0, 100.0);
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
                    value = MathUtils.Clamp(value, 150.0, 800.0);
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
                    value = MathUtils.Clamp(value, 400, 4000);
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

            private AsyncCommand _updateAmbientShadowCommand;

            public AsyncCommand UpdateAmbientShadowCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async o => {
                if (Renderer?.AmbientShadowSizeChanged == true) {
                    AmbientShadowSizeSaveCommand.Execute(null);

                    if (Renderer?.AmbientShadowSizeChanged == true) return;
                }

                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Updating shadows…");

                    await Task.Run(() => {
                        if (Renderer == null) return;
                        using (var renderer = new AmbientShadowKn5ObjectRenderer(Renderer.Kn5, Car.Location)) {
                            renderer.DiffusionLevel = (float)AmbientShadowDiffusion / 100f;
                            renderer.SkyBrightnessLevel = (float)AmbientShadowBrightness / 100f;
                            renderer.Iterations = AmbientShadowIterations;
                            renderer.HideWheels = AmbientShadowHideWheels;

                            renderer.Initialize();
                            renderer.Shot();
                        }
                    });
                    
                    waiting.Report("Reloading textures…");

                    foreach (var s in new[] {
                        "body_shadow.png",
                        "tyre_0_shadow.png",
                        "tyre_1_shadow.png",
                        "tyre_2_shadow.png",
                        "tyre_3_shadow.png"
                    }) {
                        if (Renderer == null) return;
                        await Renderer.UpdateTextureAsync(Path.Combine(Car.Location, s));
                    }

                    GC.Collect();
                }
            }));

            private RelayCommand _ambientShadowSizeSaveCommand;

            public RelayCommand AmbientShadowSizeSaveCommand => _ambientShadowSizeSaveCommand ?? (_ambientShadowSizeSaveCommand = new RelayCommand(o => {
                if (Renderer == null || File.Exists(Path.Combine(Car.Location, "data.acd")) && ModernDialog.ShowMessage(
                        "Size of the shadow will be saved to “data/ambient_shadows.ini”, but data is encrypted, so it won’t have any affect. Continue?",
                        "Data is Encrypted", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

                var data = Path.Combine(Car.Location, "data");
                Directory.CreateDirectory(data);
                new IniFile {
                    ["SETTINGS"] = {
                        ["WIDTH"] = Renderer.AmbientShadowWidth,
                        ["LENGTH"] = Renderer.AmbientShadowLength,
                    }
                }.Save(Path.Combine(data, "ambient_shadows.ini"));

                Renderer.AmbientShadowSizeChanged = false;
            }, o => Renderer?.AmbientShadowSizeChanged == true));

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
                    Kn5.FbxConverterLocation = AppAddonsManager.Instance.GetAddonFilename("FbxConverter", "FbxConverter.exe");

                    var destination = Path.Combine(Car.Location, "unpacked");

                    using (var waiting = new WaitingDialog("Exporting…")) {
                        await Task.Delay(1);

                        if (FileUtils.Exists(destination) &&
                                MessageBox.Show("Folder “unpacked” already exists. Overwrite files?", "Destination", MessageBoxButton.YesNo) == MessageBoxResult.No) {
                            var temp = destination;
                            var i = 1;
                            do {
                                destination = $"{temp}-{i++}";
                            } while (FileUtils.Exists(destination));
                        }

                        var name = Renderer.Kn5.RootNode.Name.StartsWith("FBX: ") ? Renderer.Kn5.RootNode.Name.Substring(5) :
                                "model.fbx";
                        Directory.CreateDirectory(destination);
                        await Renderer.Kn5.ExportFbxWithIniAsync(Path.Combine(destination, name), waiting, waiting.CancellationToken);

                        var textures = Path.Combine(destination, "texture");
                        Directory.CreateDirectory(textures);
                        await Renderer.Kn5.ExportTexturesAsync(textures, waiting, waiting.CancellationToken);
                    }

                    Process.Start(destination);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t unpack KN5", e);
                }
            }, o => SettingsHolder.Common.DeveloperMode && AppAddonsManager.Instance.IsAddonEnabled("FbxConverter") && Renderer?.Kn5 != null));
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

                ShowMessage($@"Object name: [b]{obj.Name}[/b]
Class: [b]{obj.NodeClass.GetDescription()}[/b]

Active: [b]{obj.Active}[/b]
Renderable: [b]{obj.IsRenderable}[/b]
Visible: [b]{obj.IsVisible}[/b]

Transparent: [b]{obj.IsTransparent}[/b]
Casting Shadows: [b]{obj.CastShadows}[/b]

Layer: [b]{obj.Layer}[/b]
LOD In: [b]{obj.LodIn}[/b]
LOD In: [b]{obj.LodOut}[/b]

Material: [b]{Renderer.SelectedMaterial?.Name ?? "?"}[/b]", obj.Name);
            }, o => Renderer?.SelectedObject != null));

            private static string MaterialPropertyToString(Kn5Material.ShaderProperty property) {
                if (property.ValueD.Any(x => !Equals(x, 0f))) return $"({property.ValueD.JoinToString(", ")})";
                if (property.ValueC.Any(x => !Equals(x, 0f))) return $"({property.ValueC.JoinToString(", ")})";
                if (property.ValueB.Any(x => !Equals(x, 0f))) return $"({property.ValueB.JoinToString(", ")})";
                return property.ValueA.ToInvariantString();
            }

            private RelayCommand _viewMaterialCommand;

            public RelayCommand ViewMaterialCommand => _viewMaterialCommand ?? (_viewMaterialCommand = new RelayCommand(o => {
                var material = Renderer?.SelectedMaterial;
                if (material == null) return;

                ShowMessage($@"Material name: [b]{material.Name}[/b]
Shader: [b]{material.ShaderName}[/b]

Blend mode: [b]{material.BlendMode.GetDescription()}[/b]
Alpha test: [b]{material.AlphaTested}[/b]
Depth mode: [b]{material.DepthMode.GetDescription()}[/b]{
        (material.ShaderProperties.Any() ? "\n\nShader properties:\n" : "")
}{material.ShaderProperties.Select(x => $"    • {x.Name}: [b]{MaterialPropertyToString(x)}[/b]").JoinToString("\n")}{
        (material.TextureMappings.Any() ? "\n\nTextures:\n" : "")
}{material.TextureMappings.Select(x => $"    • {x.Name}: [b]{x.Texture}[/b]").JoinToString("\n")}", material.Name);
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
