using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.CustomShowroom {
    public partial class LiteShowroomTools {
        public LiteShowroomTools(ForwardKn5ObjectRenderer renderer, CarObject car, string skinId) {
            DataContext = new LiteShowroomToolsViewModel(renderer, car, skinId);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.PreviewSkinCommand, new KeyGesture(Key.PageUp)),
                new InputBinding(Model.NextSkinCommand, new KeyGesture(Key.PageDown)),
                new InputBinding(Model.Car.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
                new InputBinding(Model.OpenSkinDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control))
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
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void Timer_Tick(object sender, EventArgs e) {
            Model.OnTick();
        }

        private LiteShowroomToolsViewModel Model => (LiteShowroomToolsViewModel)DataContext;

        public class LiteShowroomToolsViewModel : NotifyPropertyChanged {
            public ForwardKn5ObjectRenderer Renderer { get; }

            public CarObject Car { get; }

            private CarSkinObject _skin;

            public CarSkinObject Skin {
                get { return _skin; }
                set {
                    if (Equals(value, _skin)) return;
                    _skin = value;
                    OnPropertyChanged();

                    Renderer.SelectSkin(value?.Id);
                }
            }

            private class SaveableData {
                public double AmbientShadowDiffusion, AmbientShadowBrightness;
                public int AmbientShadowIterations;
                public bool AmbientShadowHideWheels;
            }
            protected ISaveHelper Saveable { set; get; }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            public LiteShowroomToolsViewModel(ForwardKn5ObjectRenderer renderer, CarObject carObject, string skinId) {
                Renderer = renderer;
                renderer.PropertyChanged += Renderer_PropertyChanged;
                Renderer.Tick += Renderer_Tick;

                Car = carObject;
                Skin = skinId == null ? Car.SelectedSkin : Car.GetSkinById(skinId);
                Car.SkinsManager.EnsureLoadedAsync().Forget();

                Saveable = new SaveHelper<SaveableData>("__LiteShowroomTools", () => new SaveableData {
                    AmbientShadowDiffusion = AmbientShadowDiffusion,
                    AmbientShadowBrightness = AmbientShadowBrightness,
                    AmbientShadowIterations = AmbientShadowIterations,
                    AmbientShadowHideWheels = AmbientShadowHideWheels,
                }, o => {
                    AmbientShadowDiffusion = o.AmbientShadowDiffusion;
                    AmbientShadowBrightness = o.AmbientShadowBrightness;
                    AmbientShadowIterations = o.AmbientShadowIterations;
                    AmbientShadowHideWheels = o.AmbientShadowHideWheels;
                }, () => {
                    AmbientShadowDiffusion = 40.0;
                    AmbientShadowBrightness = 400.0;
                    AmbientShadowIterations = 2000;
                    AmbientShadowHideWheels = false;
                });
                Saveable.Init();
            }

            private void Renderer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(Renderer.CurrentSkin):
                        Skin = Car.GetSkinById(Renderer.CurrentSkin);
                        break;
                }
            }

            private void Renderer_Tick(object sender, AcTools.Render.Base.TickEventArgs args) {}

            public void OnTick() {}

            #region Ambient Shadows
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
                }
            }

            private AsyncCommand _updateAmbientShadowCommand;

            public AsyncCommand UpdateAmbientShadowCommand => _updateAmbientShadowCommand ?? (_updateAmbientShadowCommand = new AsyncCommand(async o => {
                await Task.Run(() => {
                    using (var renderer = new AmbientShadowKn5ObjectRenderer(Renderer.Kn5)) {
                        renderer.DiffusionLevel = (float)AmbientShadowDiffusion / 100f;
                        renderer.SkyBrightnessLevel = (float)AmbientShadowBrightness / 100f;
                        renderer.Iterations = AmbientShadowIterations;
                        renderer.HideWheels = AmbientShadowHideWheels;
                        
                        renderer.Initialize();
                        renderer.Shot();
                    }
                });
                
                foreach (var s in new[] {
                    "body_shadow.png",
                    "tyre_0_shadow.png",
                    "tyre_1_shadow.png",
                    "tyre_2_shadow.png",
                    "tyre_3_shadow.png"
                }) {
                    await Renderer.UpdateTextureAsync(Path.Combine(Car.Location, s));
                }
            }));
            #endregion

            #region Commands
            private RelayCommand _nextSkinCommand;

            public RelayCommand NextSkinCommand => _nextSkinCommand ?? (_nextSkinCommand = new RelayCommand(o => {
                Renderer.SelectNextSkin();
            }));

            private RelayCommand _previewSkinCommand;

            public RelayCommand PreviewSkinCommand => _previewSkinCommand ?? (_previewSkinCommand = new RelayCommand(o => {
                Renderer.SelectPreviousSkin();
            }));

            private RelayCommand _openSkinDirectoryCommand;

            public RelayCommand OpenSkinDirectoryCommand => _openSkinDirectoryCommand ?? (_openSkinDirectoryCommand = new RelayCommand(o => {
                Skin.ViewInExplorer();
            }, o => Skin != null));
            #endregion
        }
    }
}
