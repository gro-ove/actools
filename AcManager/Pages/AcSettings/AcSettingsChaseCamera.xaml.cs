using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AcManager.Tools;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsChaseCamera {
        public AcSettingsChaseCamera() {
            InitializeComponent();
            DataContext = new ViewModel();
            Model.PropertyChanged += (sender, args) => UpdateCamera();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);

            AcSettingsHolder.Video.SubscribeWeak(this, OnVideoPropertyChanged);
            UpdateResolution();

            AcSettingsHolder.CameraChase.First.SubscribeWeak(OnCameraPropertyChanged);
            AcSettingsHolder.CameraChase.Second.SubscribeWeak(OnCameraPropertyChanged);

            this.OnActualUnload(() => {
                AcSettingsHolder.CameraChase.First.UnsubscribeWeak(OnCameraPropertyChanged);
                AcSettingsHolder.CameraChase.Second.UnsubscribeWeak(OnCameraPropertyChanged);
                DisableRenderer().Ignore();
            });
        }

        private void OnCameraPropertyChanged(object s, PropertyChangedEventArgs e) {
            if (ReferenceEquals(s, Model.SelectedCamera)) {
                UpdateCamera();
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, EventArgs e) {
            _loaded = true;
            _enableRendererBusy.Task(EnableRenderer);
        }

        private void OnUnloaded(object sender, EventArgs e) {
            _loaded = false;
            _enableRendererBusy.Task(DisableRenderer);
        }

        private D3DImageEx _imageEx;

        private IntPtr _lastTarget;
        private int _setCount;
        private DarkKn5ObjectRenderer _renderer;
        private Vector3 _carOffset, _lookAt;
        private double _carLength;
        private string _carId;

        private Busy _enableRendererBusy = new Busy();

        private async Task EnableRenderer() {
            if (!_loaded) {
                await DisableRenderer();
                return;
            }

            try {
                _carId = ValuesStorage.Storage.GetObject<JObject>("__QuickDrive_Main")?.GetStringValueOnly("CarId") ?? @"abarth500";

                var car = CarsManager.Instance.GetById(_carId);
                if (_imageEx != null || car == null) return;

                Progress.IsActive = true;
                var panoramaBg = new Uri("pack://application:,,,/Content Manager;component/Assets/Img/ShowroomPanoramaExample.jpg", UriKind.Absolute);
                var renderer = new DarkKn5ObjectRenderer(CarDescription.FromDirectory(car.Location, car.AcdData)) {
                    WpfMode = true,
                    UseMsaa = false,
                    VisibleUi = false,
                    TryToGuessCarLights = false,
                    LoadCarLights = false,
                    AnyGround = true,
                    BackgroundColor = System.Drawing.Color.FromArgb(0x444444),
                    BackgroundBrightness = 1f,
                    LightBrightness = 0f,
                    AmbientDown = System.Drawing.Color.FromArgb(0x444444),
                    AmbientUp = System.Drawing.Color.FromArgb(0xffffff),
                    AmbientBrightness = 4f,
                    Light = Vector3.Normalize(new Vector3(-0.1f, 10, -0.1f)),
                    ShadowMapSize = 512,
                    EnableShadows = false,
                    AutoRotate = false,
                    AutoAdjustTarget = false,
                    UseDof = true,
                    UseAccumulationDof = true,
                    AccumulationDofApertureSize = 0f,
                    AccumulationDofIterations = 20,
                    AccumulationDofBokeh = false,
                    UseFxaa = false,
                    UseSslr = true,
                    UseAo = false,
                    UseDither = true,
                    MaterialsReflectiveness = 1.5f,
                    UseCustomReflectionCubemap = true,
                    CustomReflectionCubemap = Application.GetResourceStream(panoramaBg)?.Stream.ReadAsBytesAndDispose(),
                    CubemapAmbientWhite = false,
                    CubemapAmbient = 0.6f,
                };

                var data = car.AcdData;
                if (data != null) {
                    var carBasic = data.GetIniFile("car.ini")["BASIC"];
                    _carLength = carBasic.GetVector3F("INERTIA").ToVector3().Z;

                    var suspensions = data.GetIniFile("suspensions.ini");
                    var suspensionsBasic = suspensions["BASIC"];
                    var go = carBasic.GetVector3F("GRAPHICS_OFFSET").ToVector3();
                    var center = suspensionsBasic.GetFloat("WHEELBASE", 2.5f) * suspensionsBasic.GetFloat("CG_LOCATION", 0.5f);
                    _carOffset = new Vector3(go.X, go.Y - suspensions["REAR"].GetFloat("BASEY", 0.25f), go.Z + center);
                    _lookAt = new Vector3(go.X, go.Y - suspensions["FRONT"].GetFloat("BASEY", 0.25f), go.Z + center);
                }

                await Task.Run(() => renderer.Initialize());
                renderer.SetCameraHigher = false;
                SetRendererSize(renderer);

                if (renderer.CarNode != null) {
                    // renderer.SelectSkin(car.SelectedSkin?.Id);
                    renderer.CarNode.BrakeLightsEnabled = true;
                    renderer.CarNode.CockpitLrActive = true;
                    renderer.CarNode.CurrentLod = renderer.CarNode.LodsCount > 1 ? 1 : 0;
                }

                await Task.Run(() => renderer.Draw());
                _renderer = renderer;

                _imageEx = new D3DImageEx();
                Scene.Source = _imageEx;

                _setCount = 0;
                _lastTarget = IntPtr.Zero;

                CompositionTargetEx.Rendering += OnRendering;
                UpdateCamera();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t display chase camera preview", e);
            } finally {
                Progress.IsActive = false;
            }
        }

        private void UpdateCamera() {
            var camera = Model.SelectedCamera;
            var origin = new Vector3(0, (float)camera.Height, (float)-camera.Distance) - _carOffset;
            var look = new Vector3(0, 0, (float)_carLength) - _lookAt;
            var pitch = Vector3.TransformNormal(look - origin, SlimDX.Matrix.RotationAxis(Vector3.UnitX, (float)-camera.Pitch));
            _renderer.SetCamera(origin, origin + pitch, 60f.ToRadians(), pitch.Z < 0 ? MathF.PI : 0);
        }

        private async Task DisableRenderer() {
            if (_loaded) {
                await EnableRenderer();
                return;
            }

            if (_imageEx == null) return;

            try {
                DisposeHelper.Dispose(ref _renderer);

                CompositionTargetEx.Rendering -= OnRendering;
                Scene.Source = null;

                _imageEx.Lock();
                _imageEx.SetBackBufferEx(D3DResourceTypeEx.ID3D11Texture2D, IntPtr.Zero);
                _imageEx.Unlock();
                _imageEx = null;
            } catch (Exception e) {
                MessageBox.Show(e.ToString());
            }
        }

        private static TimeSpan _last = TimeSpan.Zero;

        private void OnRendering(object sender, EventArgs e) {
            var args = (RenderingEventArgs)e;

            if (args.RenderingTime == _last) return;
            _last = args.RenderingTime;

            var renderer = _renderer;
            if (_imageEx == null || renderer == null
                    || !renderer.IsDirty && _lastTarget != IntPtr.Zero && _setCount > 3) return;

            renderer.Draw();

            var target = renderer.GetRenderTarget();
            if (target != _lastTarget || _setCount < 3) {
                _imageEx.SetBackBufferEx(D3DResourceTypeEx.ID3D11Texture2D, target);
                _setCount++;
                _lastTarget = target;
            }

            _imageEx.Lock();
            _imageEx.AddDirtyRect(new Int32Rect {
                X = 0,
                Y = 0,
                Height = _imageEx.PixelHeight,
                Width = _imageEx.PixelWidth
            });
            _imageEx.Unlock();
        }

        private void SetRendererSize([NotNull] BaseRenderer renderer) {
            renderer.Width = Wrapper.Width.RoundToInt();
            renderer.Height = Wrapper.Height.RoundToInt();
        }

        private void OnVideoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            UpdateResolution();
        }

        private void UpdateResolution() {
            var width = (AcSettingsHolder.Video.Resolution?.Width ?? 1920).Clamp(100, 10000);
            var height = (AcSettingsHolder.Video.Resolution?.Height ?? 1080).Clamp(100, 10000);
            Wrapper.Height = height * 400d / width;

            if (_renderer != null) {
                SetRendererSize(_renderer);
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public CameraChaseSettings.Camera[] Cameras { get; }

            internal ViewModel() {
                Cameras = new[] {
                    CameraChase.First,
                    CameraChase.Second
                };

                SelectedCamera = Cameras.ArrayElementAtOrDefault(ValuesStorage.Get(".AcSettingsChaseCamera.Selected", 0)) ?? CameraChase.First;
            }

            private CameraChaseSettings.Camera _selectedCamera;

            public CameraChaseSettings.Camera SelectedCamera {
                get => _selectedCamera;
                set => Apply(value, ref _selectedCamera, () => { ValuesStorage.Set(".AcSettingsChaseCamera.Selected", Cameras.IndexOf(value)); });
            }

            public CameraChaseSettings CameraChase => AcSettingsHolder.CameraChase;
        }
    }
}