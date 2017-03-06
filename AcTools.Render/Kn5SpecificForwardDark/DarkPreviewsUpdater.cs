using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Temporary;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using ThreadPool = AcTools.Render.Base.Utils.ThreadPool;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkPreviewsOptions {
        public string PreviewName = "preview.jpg";

        /// <summary>
        /// Either ID or KN5’s filename.
        /// </summary>
        public string Showroom = null;

        public double SsaaMultiplier = 4d;
        public int PreviewWidth = CommonAcConsts.PreviewWidth;
        public int PreviewHeight = CommonAcConsts.PreviewHeight;
        public bool UseFxaa = false;
        public bool UseMsaa = false;
        public int MsaaSampleCount = 4;
        public double BloomRadiusMultiplier = 1d;
        public bool HardwareDownscale = true;

        public bool WireframeMode = false;
        public bool MeshDebugMode = false;
        public bool SuspensionDebugMode = false;

        public bool HeadlightsEnabled = false;
        public bool BrakeLightsEnabled = false;
        public double SteerAngle = 0d;
        public bool LeftDoorOpen = false;
        public bool RightDoorOpen = false;

        public double[] CameraPosition = { 3.194, 0.342, 13.049 };
        public double[] CameraLookAt = { 2.945, 0.384, 12.082 };
        public double CameraFov = 9d;

        public bool AlignCar = false;
        public bool AlignCamera = false;
        public double[] AlignCameraOffset = { 0.0, 0.0, 0.0 };

        public bool FlatMirror = false;
        public bool FlatMirrorBlurred = false;
        public double FlatMirrorReflectiveness = 1d;

        public bool UseSslr = false;
        public bool UseSsao = false;
        public bool EnableShadows = true;
        public bool UsePcss = true;
        public int ShadowMapSize = 4096;
        public double ReflectionMultiplier = 1d;
        public bool ReflectionCubemapAtCamera = true;
        public bool NoShadowsWithReflections = true;

        public Color BackgroundColor = Color.Black;
        public Color LightColor = Color.FromArgb(0xffffff);
        public Color AmbientUp = Color.FromArgb(0xb4b496);
        public Color AmbientDown = Color.FromArgb(0x96b4b4);

        public double AmbientBrightness = 2d;
        public double LightBrightness = 1.5;
        public double[] LightDirection = { 0.2, 1.0, 0.8 };

        public bool UseToneMapping = false;
        public double ToneExposure = 0.8;
        public double ToneGamma = 1.0;
        public double ToneWhitePoint = 1.66;
        public string ColorGradingFilename = null;

        public bool DelayedConvertation = true;
    }

    public class DarkPreviewsUpdater : IDisposable {
        private readonly string _acRoot;
        private readonly DarkPreviewsOptions _options;

        private DarkKn5ObjectRenderer _renderer;
        private string _carId;

        private ThreadPool _convertationThreadPool;

        public DarkPreviewsUpdater(string acRoot, DarkPreviewsOptions options = null) {
            _acRoot = acRoot;
            _options = options ?? new DarkPreviewsOptions();
        }

        private CarDescription GetCarDescription(string carId, [CanBeNull] DataWrapper carData) {
            var carDirectory = FileUtils.GetCarDirectory(_acRoot, carId);
            var carKn5 = carData == null ? FileUtils.GetMainCarFilename(carDirectory) : FileUtils.GetMainCarFilename(carDirectory, carData);
            return new CarDescription(carKn5, carDirectory);
        }

        private static Vector3 ToVector3(double[] v) {
            return new Vector3((float)v[0], (float)v[1], (float)v[2]);
        }

        private void UpdateCamera() {
            _renderer.SetCamera(
                    ToVector3(_options.CameraPosition), ToVector3(_options.CameraLookAt),
                    (float)(MathF.PI / 180f * _options.CameraFov));
            
            if (_options.AlignCar) {
                _renderer.AlignCar();
            }

            if (_options.AlignCamera) {
                _renderer.AlignCamera(ToVector3(_options.AlignCameraOffset));
            }
        }

        private static DarkKn5ObjectRenderer CreateRenderer(string acRoot, DarkPreviewsOptions options, CarDescription initialCar) {
            var showroom = options.Showroom;

            if (showroom != null && !File.Exists(showroom)) {
                var kn5 = Path.Combine(FileUtils.GetShowroomDirectory(acRoot, showroom), $"{showroom}.kn5");
                showroom = File.Exists(kn5) ? kn5 : null;
            }

            var renderer = new DarkKn5ObjectRenderer(initialCar, showroom) {
                // Obvious fixed settings
                AutoRotate = false,
                AutoAdjustTarget = false,
                AsyncTexturesLoading = false,

                // Size-related options
                Width = (int)(options.PreviewWidth * options.SsaaMultiplier),
                Height = (int)(options.PreviewHeight * options.SsaaMultiplier),
                UseFxaa = options.UseFxaa,
                UseMsaa = options.UseMsaa,
                MsaaSampleCount = options.MsaaSampleCount,
                KeepFxaaWhileShooting = true,
                BloomRadiusMultiplier = (float)(options.SsaaMultiplier * options.BloomRadiusMultiplier),

                // Switches
                ShowWireframe = options.WireframeMode,
                MeshDebug = options.MeshDebugMode,
                SuspensionDebug = options.SuspensionDebugMode,

                // Flat mirror
                FlatMirror = options.FlatMirror,
                FlatMirrorBlurred = options.FlatMirrorBlurred,
                FlatMirrorReflectiveness = (float)options.FlatMirrorReflectiveness,

                // Cool effects
                EnableShadows = options.EnableShadows,
                UsePcss = options.UsePcss,
                ShadowMapSize = options.ShadowMapSize,
                UseSslr = options.UseSslr,
                UseSsao = options.UseSsao,
                MaterialsReflectiveness = (float)options.ReflectionMultiplier,
                ReflectionCubemapAtCamera = options.ReflectionCubemapAtCamera,
                ReflectionsWithShadows = !options.NoShadowsWithReflections,

                // Colors
                BackgroundColor = options.BackgroundColor,
                LightColor = options.LightColor,
                AmbientUp = options.AmbientUp,
                AmbientDown = options.AmbientDown,

                // Brightnesses
                AmbientBrightness = (float)options.AmbientBrightness,
                LightBrightness = (float)options.LightBrightness,
                Light = ToVector3(options.LightDirection),

                // Color
                UseToneMapping = options.UseToneMapping,
                ToneExposure = (float)options.ToneExposure,
                ToneGamma = (float)options.ToneGamma,
                ToneWhitePoint = (float)options.ToneWhitePoint,
                ColorGradingFilename = options.ColorGradingFilename,
            };

            renderer.Initialize();

            var car = renderer.CarNode;
            if (car != null) {
                car.LightsEnabled = options.HeadlightsEnabled;
                car.BrakeLightsEnabled = options.BrakeLightsEnabled;
                car.SteerDeg = (float)options.SteerAngle;
                car.LeftDoorOpen = options.LeftDoorOpen;
                car.RightDoorOpen = options.RightDoorOpen;
                car.OnTick(float.MaxValue);
            }

            return renderer;
        }

        private long _processingNow;

        private void ProcessConvertation(Action action, Action disposal) {
            if (!_options.DelayedConvertation) {
                try {
                    action.Invoke();
                } finally {
                    disposal.Invoke();
                }
                return;
            }
            
            if (_convertationThreadPool == null) {
                _convertationThreadPool = new ThreadPool("Previews Convertation Thread", 4, ThreadPriority.BelowNormal);
            }

            Interlocked.Increment(ref _processingNow);
            _convertationThreadPool.QueueTask(() => {
                try {
                    action.Invoke();
                } catch(Exception e) {
                    Logging.Error("Convertation error: " + e);
                } finally {
                    disposal.Invoke();
                    Interlocked.Decrement(ref _processingNow);
                }
            });
        }

        private int? _approximateSize;

        private void ShotInner(string carId, string skinId, string destination) {
            if (destination == null) {
                destination = Path.Combine(FileUtils.GetCarSkinDirectory(_acRoot, carId, skinId), _options.PreviewName);
            }

            var shotStream = new MemoryStream(_approximateSize ?? 100000);

            _renderer.Shot(1d, _options.HardwareDownscale ? 1d / _options.SsaaMultiplier : 1d, shotStream, true);
            if (!_approximateSize.HasValue || _approximateSize < shotStream.Position) {
                _approximateSize = (int)(shotStream.Position * 1.2);
            }

            shotStream.Position = 0;
            ProcessConvertation(() => {
                using (var stream = File.Open(destination, FileMode.Create, FileAccess.ReadWrite)) {
                    ImageUtils.Convert(shotStream, stream,
                            _options.HardwareDownscale ? (Size?)null : new Size(_options.PreviewWidth, _options.PreviewHeight));
                }
            }, () => {
                shotStream.Dispose();
            });
        }

        private Task ShotInnerAsync(string carId, string skinId, string destination) {
            return Task.Run(() => ShotInner(carId, skinId, destination));
        }

        public void Shot(string carId, string skinId, string destination = null, DataWrapper carData = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = CreateRenderer(_acRoot, _options, GetCarDescription(carId, carData));
                } else {
                    _renderer.SetCar(GetCarDescription(carId, carData), skinId);
                    _renderer.CarNode?.OnTick(float.MaxValue);
                }
                _carId = carId;
                UpdateCamera();
            } else {
                _renderer.SelectSkin(skinId);
            }

            ShotInner(carId, skinId, destination);
        }

        public async Task ShotAsync(string carId, string skinId, string destination = null, DataWrapper carData = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = await Task.Run(() => CreateRenderer(_acRoot, _options, GetCarDescription(carId, carData))).ConfigureAwait(false);
                } else {
                    _renderer.SetCar(GetCarDescription(carId, carData), skinId);
                }
                _carId = carId;
            } else {
                _renderer.SelectSkin(skinId);
            }

            await ShotInnerAsync(carId, skinId, destination).ConfigureAwait(false);
        }

        public async Task WaitForProcessing() {
            while (Interlocked.Read(ref _processingNow) > 0) {
                await Task.Delay(100);
            }
        }

        public void Dispose() {
            while (Interlocked.Read(ref _processingNow) > 0) {
                Thread.Sleep(100);
            }
            
            DisposeHelper.Dispose(ref _renderer);
            DisposeHelper.Dispose(ref _convertationThreadPool);
        }
    }
}