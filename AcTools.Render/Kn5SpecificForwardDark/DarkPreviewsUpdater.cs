using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Temporary;
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
        [CanBeNull]
        public string Showroom = null;

        public double SsaaMultiplier = 4d;
        public int PreviewWidth = CommonAcConsts.PreviewWidth;
        public int PreviewHeight = CommonAcConsts.PreviewHeight;
        public bool UseFxaa = false;
        public bool UseSmaa = false;
        public bool UseMsaa = false;
        public int MsaaSampleCount = 4;
        public bool SoftwareDownsize = false;

        public bool WireframeMode = false;
        public bool MeshDebugMode = false;
        public bool SuspensionDebugMode = false;

        public bool HeadlightsEnabled = false;
        public bool BrakeLightsEnabled = false;
        public double SteerDeg = 0d;
        public bool LeftDoorOpen = false;
        public bool RightDoorOpen = false;
        public bool ShowDriver = false;

        public double[] CameraPosition = { 3.194, 0.342, 13.049 };
        public double[] CameraLookAt = { 2.945, 0.384, 12.082 };
        public double CameraFov = 9d;

        public bool AlignCar = true;
        public bool AlignCameraHorizontally = true;
        public bool AlignCameraVertically = false;
        public bool AlignCameraHorizontallyOffsetRelative = true;
        public bool AlignCameraVerticallyOffsetRelative = true;
        public double AlignCameraHorizontallyOffset = 0.3;
        public double AlignCameraVerticallyOffset = 0.0;

        public bool FlatMirror = false;
        public bool FlatMirrorBlurred = false;
        public double FlatMirrorReflectiveness = 1d;

        public float CubemapAmbient = 0.5f;
        public bool CubemapAmbientWhite = true;
        public bool UseBloom = true;
        public bool UseSslr = false;
        public bool UseAo = false;
        public AoType AoType = AoType.Ssao;
        public bool EnableShadows = true;
        public bool UsePcss = true;
        public int ShadowMapSize = 4096;
        public bool ReflectionCubemapAtCamera = true;
        public bool ReflectionsWithShadows = false;

        public Color BackgroundColor = Color.Black;
        public Color LightColor = Color.FromArgb(0xffffff);
        public Color AmbientUp = Color.FromArgb(0xb4b496);
        public Color AmbientDown = Color.FromArgb(0x96b4b4);

        public double AmbientBrightness = 2d;
        public double BackgroundBrightness = 1d;
        public double LightBrightness = 1.5;
        public double[] LightDirection = { 0.2, 1.0, 0.8 };

        public ToneMappingFn ToneMapping = ToneMappingFn.None;
        public bool UseColorGrading = false;
        public double ToneExposure = 0.8;
        public double ToneGamma = 1.0;
        public double ToneWhitePoint = 1.66;
        public byte[] ColorGradingData = null;

        public double MaterialsReflectiveness = 1.2;
        public double BloomRadiusMultiplier = 0.8d;
        public double PcssSceneScale = 0.06d;
        public double PcssLightScale = 2d;
        public double SsaoOpacity = 0.3d;

        public bool DelayedConvertation = true;

        public bool UseDof = false;
        public double DofFocusPlane = 1.6;
        public double DofScale = 1d;
        public bool UseAccumulationDof = false;
        public int AccumulationDofIterations = 300;
        public double AccumulationDofApertureSize = 0.01;

        #region Checksum
        private static int GetHashCode(double[] array) {
            if (array == null) return 0;
            unchecked {
                var hashCode = 0;
                for (var i = 0; i < array.Length; i++) {
                    hashCode = (hashCode * 397) ^ array[i].GetHashCode();
                }
                return hashCode;
            }
        }

        private static int GetHashCode(byte[] array) {
            if (array == null) return 0;
            unchecked {
                var hashCode = 0;
                for (var i = 0; i < array.Length; i += 1000) {
                    hashCode = (hashCode * 397) ^ array[i].GetHashCode();
                }
                return hashCode;
            }
        }

        public string FixedChecksum { get; set; }

        public string GetChecksum() {
            if (FixedChecksum != null) return FixedChecksum;

            unchecked {
                long hashCode = Showroom?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ SsaaMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ PreviewWidth;
                hashCode = (hashCode * 397) ^ PreviewHeight;
                hashCode = (hashCode * 397) ^ UseFxaa.GetHashCode();
                hashCode = (hashCode * 397) ^ UseSmaa.GetHashCode();
                hashCode = (hashCode * 397) ^ UseMsaa.GetHashCode();
                hashCode = (hashCode * 397) ^ MsaaSampleCount;
                hashCode = (hashCode * 397) ^ SoftwareDownsize.GetHashCode();
                hashCode = (hashCode * 397) ^ WireframeMode.GetHashCode();
                hashCode = (hashCode * 397) ^ MeshDebugMode.GetHashCode();
                hashCode = (hashCode * 397) ^ SuspensionDebugMode.GetHashCode();
                hashCode = (hashCode * 397) ^ HeadlightsEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ BrakeLightsEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ SteerDeg.GetHashCode();
                hashCode = (hashCode * 397) ^ LeftDoorOpen.GetHashCode();
                hashCode = (hashCode * 397) ^ RightDoorOpen.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowDriver.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(CameraPosition);
                hashCode = (hashCode * 397) ^ GetHashCode(CameraLookAt);
                hashCode = (hashCode * 397) ^ CameraFov.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCar.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontally.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVertically.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontallyOffsetRelative.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVerticallyOffsetRelative.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraHorizontallyOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignCameraVerticallyOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ FlatMirror.GetHashCode();
                hashCode = (hashCode * 397) ^ FlatMirrorBlurred.GetHashCode();
                hashCode = (hashCode * 397) ^ FlatMirrorReflectiveness.GetHashCode();
                hashCode = (hashCode * 397) ^ CubemapAmbient.GetHashCode();
                hashCode = (hashCode * 397) ^ CubemapAmbientWhite.GetHashCode();
                hashCode = (hashCode * 397) ^ UseBloom.GetHashCode();
                hashCode = (hashCode * 397) ^ UseSslr.GetHashCode();
                hashCode = (hashCode * 397) ^ UseAo.GetHashCode();
                hashCode = (hashCode * 397) ^ AoType.GetHashCode();
                hashCode = (hashCode * 397) ^ EnableShadows.GetHashCode();
                hashCode = (hashCode * 397) ^ UsePcss.GetHashCode();
                hashCode = (hashCode * 397) ^ ShadowMapSize;
                hashCode = (hashCode * 397) ^ ReflectionCubemapAtCamera.GetHashCode();
                hashCode = (hashCode * 397) ^ ReflectionsWithShadows.GetHashCode();
                hashCode = (hashCode * 397) ^ BackgroundColor.GetHashCode();
                hashCode = (hashCode * 397) ^ LightColor.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientUp.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientDown.GetHashCode();
                hashCode = (hashCode * 397) ^ AmbientBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ BackgroundBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ LightBrightness.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(LightDirection);
                hashCode = (hashCode * 397) ^ UseColorGrading.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneExposure.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneGamma.GetHashCode();
                hashCode = (hashCode * 397) ^ ToneWhitePoint.GetHashCode();
                hashCode = (hashCode * 397) ^ GetHashCode(ColorGradingData);
                hashCode = (hashCode * 397) ^ MaterialsReflectiveness.GetHashCode();
                hashCode = (hashCode * 397) ^ BloomRadiusMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ PcssSceneScale.GetHashCode();
                hashCode = (hashCode * 397) ^ PcssLightScale.GetHashCode();
                hashCode = (hashCode * 397) ^ SsaoOpacity.GetHashCode();
                return Convert.ToBase64String(BitConverter.GetBytes(hashCode)).TrimEnd('=');
            }
        }
        #endregion
    }

    public class DarkPreviewsUpdater : IDisposable {
        private readonly string _acRoot;
        private DarkPreviewsOptions _options;
        private readonly bool _existingRenderer;

        private DarkKn5ObjectRenderer _renderer;
        private string _carId;

        private ThreadPool _convertationThreadPool;

        public DarkPreviewsUpdater(string acRoot, DarkPreviewsOptions options = null, DarkKn5ObjectRenderer existingRenderer = null) {
            _acRoot = acRoot;
            _options = options ?? new DarkPreviewsOptions();

            if (existingRenderer != null) {
                _existingRenderer = true;
                _renderer = existingRenderer;
                _carId = existingRenderer.CarNode?.CarId;
            }
        }

        public void SetOptions(DarkPreviewsOptions options) {
            _options = options;

            if (_renderer != null) {
                SetRendererOptions(_renderer, options);
                SetRendererCarOptions(_renderer, options);
                UpdateCamera();
            }
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

            _renderer.AlignCamera(_options.AlignCameraHorizontally, (float)_options.AlignCameraHorizontallyOffset,
                    _options.AlignCameraHorizontallyOffsetRelative,
                    _options.AlignCameraVertically, (float)_options.AlignCameraVerticallyOffset, _options.AlignCameraVerticallyOffsetRelative);
        }

        private static DarkKn5ObjectRenderer CreateRenderer(string acRoot, DarkPreviewsOptions options, CarDescription initialCar, string initialSkinId) {
            var showroom = options.Showroom;

            if (showroom != null && !File.Exists(showroom)) {
                var kn5 = Path.Combine(FileUtils.GetShowroomDirectory(acRoot, showroom), $"{showroom}.kn5");
                showroom = File.Exists(kn5) ? kn5 : null;
            }

            var renderer = new DarkKn5ObjectRenderer(initialCar, showroom);
            SetRendererOptions(renderer, options);

            renderer.SelectSkin(initialSkinId);
            renderer.Initialize();
            SetRendererCarOptions(renderer, options);

            return renderer;
        }

        private static void SetRendererOptions(DarkKn5ObjectRenderer renderer, DarkPreviewsOptions options) {
            // Obvious fixed settings
            renderer.AutoRotate = false;
            renderer.AutoAdjustTarget = false;
            renderer.AsyncTexturesLoading = false;

            // UI should be off!
            renderer.VisibleUi = false;

            // Size-related options
            renderer.Width = (int)(options.PreviewWidth * options.SsaaMultiplier);
            renderer.Height = (int)(options.PreviewHeight * options.SsaaMultiplier);
            renderer.UseFxaa = options.UseFxaa;
            renderer.UseSmaa = options.UseSmaa;
            renderer.UseMsaa = options.UseMsaa;
            renderer.MsaaSampleCount = options.MsaaSampleCount;

            // Switches
            renderer.ShowWireframe = options.WireframeMode;
            renderer.MeshDebug = options.MeshDebugMode;

            // Flat mirror
            renderer.FlatMirror = options.FlatMirror;
            renderer.FlatMirrorBlurred = options.FlatMirrorBlurred;
            renderer.FlatMirrorReflectiveness = (float)options.FlatMirrorReflectiveness;

            // Cool effects
            renderer.CubemapAmbient = options.CubemapAmbient;
            renderer.CubemapAmbientWhite = options.CubemapAmbientWhite;
            renderer.UseBloom = options.UseBloom;
            renderer.EnableShadows = options.EnableShadows;
            renderer.UsePcss = options.UsePcss;
            renderer.ShadowMapSize = options.ShadowMapSize;
            renderer.UseSslr = options.UseSslr;
            renderer.UseAo = options.UseAo;
            renderer.AoType = options.AoType;
            renderer.ReflectionCubemapAtCamera = options.ReflectionCubemapAtCamera;
            renderer.ReflectionsWithShadows = options.ReflectionsWithShadows;

            // Colors
            renderer.BackgroundColor = options.BackgroundColor;
            renderer.LightColor = options.LightColor;
            renderer.AmbientUp = options.AmbientUp;
            renderer.AmbientDown = options.AmbientDown;

            // Brightnesses
            renderer.AmbientBrightness = (float)options.AmbientBrightness;
            renderer.BackgroundBrightness = (float)options.BackgroundBrightness;
            renderer.LightBrightness = (float)options.LightBrightness;
            renderer.Light = ToVector3(options.LightDirection);

            // Color
            renderer.ToneMapping = options.ToneMapping;
            renderer.UseColorGrading = options.UseColorGrading;
            renderer.ToneExposure = (float)options.ToneExposure;
            renderer.ToneGamma = (float)options.ToneGamma;
            renderer.ToneWhitePoint = (float)options.ToneWhitePoint;
            renderer.ColorGradingData = options.ColorGradingData;

            // Extra
            renderer.BloomRadiusMultiplier = (float)(options.SsaaMultiplier * options.BloomRadiusMultiplier);
            renderer.MaterialsReflectiveness = (float)options.MaterialsReflectiveness;
            renderer.PcssLightScale = (float)options.PcssLightScale;
            renderer.PcssSceneScale = (float)options.PcssSceneScale;
            renderer.AoOpacity = (float)options.SsaoOpacity;

            // DOF
            renderer.UseDof = options.UseDof;
            renderer.DofFocusPlane = (float)options.DofFocusPlane;
            renderer.DofScale = (float)options.DofScale;
            renderer.UseAccumulationDof = options.UseAccumulationDof;
            renderer.AccumulationDofIterations = options.AccumulationDofIterations;
            renderer.AccumulationDofApertureSize = (float)options.AccumulationDofApertureSize;
        }

        private static void SetRendererCarOptions(DarkKn5ObjectRenderer renderer, DarkPreviewsOptions options) {
            var car = renderer.CarNode;
            if (car != null) {
                car.HeadlightsEnabled = options.HeadlightsEnabled;
                car.BrakeLightsEnabled = options.BrakeLightsEnabled;
                car.SteerDeg = (float)options.SteerDeg;
                car.LeftDoorOpen = options.LeftDoorOpen;
                car.RightDoorOpen = options.RightDoorOpen;
                car.IsDriverVisible = options.ShowDriver;
                car.OnTick(float.MaxValue);
                car.BlurredNodesActive = false;
                car.CockpitLrActive = false;
                car.SeatbeltOnActive = false;

                if (options.SuspensionDebugMode) {
                    car.SuspensionDebug = true;
                }
            }
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
                } catch (Exception e) {
                    Logging.Error("Convertation error: " + e);
                } finally {
                    disposal.Invoke();
                    Interlocked.Decrement(ref _processingNow);
                }
            });
        }

        private int? _approximateSize;

        private static Action DisposeCallback(IDisposable dispose) {
            return dispose.Dispose;
        }

        private void ShotInner(string carId, string skinId, string destination, ImageUtils.ImageInformation information, Action callback) {
            if (destination == null) {
                destination = Path.Combine(FileUtils.GetCarSkinDirectory(_acRoot, carId, skinId), _options.PreviewName);
            }

            var shotStream = new MemoryStream(_approximateSize ?? 100000);

            _renderer.Shot(1d, _options.SoftwareDownsize ? 1d : 1d / _options.SsaaMultiplier, 1d, shotStream, true, null, default(CancellationToken));
            if (!_approximateSize.HasValue || _approximateSize < shotStream.Position) {
                _approximateSize = (int)(shotStream.Position * 1.2);
            }

            shotStream.Position = 0;
            ProcessConvertation(() => {
                using (var stream = File.Open(destination, FileMode.Create, FileAccess.ReadWrite)) {
                    ImageUtils.Convert(shotStream, stream,
                            _options.SoftwareDownsize ? new Size(_options.PreviewWidth, _options.PreviewHeight) : (Size?)null, exif: information);
                    callback?.Invoke();
                }
            }, DisposeCallback(shotStream));
        }

        private Task ShotInnerAsync(string carId, string skinId, string destination, ImageUtils.ImageInformation information, Action callback) {
            return Task.Run(() => ShotInner(carId, skinId, destination, information, callback));
        }

        /// <summary>
        /// Update preview.
        /// </summary>
        /// <param name="carId">Car ID.</param>
        /// <param name="skinId">Skin ID.</param>
        /// <param name="destination">Destination filename.</param>
        /// <param name="carData">Car data (provide it only if it’s already loaded, so Updater won’t load it again).</param>
        /// <param name="information">Some lines for EXIF data, optional.</param>
        /// <param name="callback">Callback in sync version? Because, with Delayed Convertation enabled, even sync version is not so sync.</param>
        public void Shot(string carId, string skinId, string destination = null, DataWrapper carData = null, ImageUtils.ImageInformation information = null,
                Action callback = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = CreateRenderer(_acRoot, _options, GetCarDescription(carId, carData), skinId);
                } else {
                    _renderer.MainSlot.SetCar(GetCarDescription(carId, carData), skinId);
                }
                _carId = carId;
                UpdateCamera();
            } else {
                _renderer.SelectSkin(skinId);
            }

            _renderer.OnTick(float.MaxValue);
            ShotInner(carId, skinId, destination, information, callback);
        }

        /// <summary>
        /// Update preview.
        /// </summary>
        /// <param name="carId">Car ID.</param>
        /// <param name="skinId">Skin ID.</param>
        /// <param name="destination">Destination filename.</param>
        /// <param name="carData">Car data (provide it only if it’s already loaded, so Updater won’t load it again).</param>
        /// <param name="information">Some lines for EXIF data, optional.</param>
        /// <param name="callback">Callback in Task version? Because, with Delayed Convertation enabled, image might be saved later.</param>
        public async Task ShotAsync(string carId, string skinId, string destination = null, DataWrapper carData = null,
                ImageUtils.ImageInformation information = null, Action callback = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = await Task.Run(() => CreateRenderer(_acRoot, _options, GetCarDescription(carId, carData), skinId)).ConfigureAwait(false);
                } else {
                    await _renderer.MainSlot.SetCarAsync(GetCarDescription(carId, carData), skinId);
                }
                _carId = carId;
                UpdateCamera();
            } else {
                _renderer.SelectSkin(skinId);
            }

            _renderer.OnTick(float.MaxValue);
            await ShotInnerAsync(carId, skinId, destination, information, callback).ConfigureAwait(false);
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

            if (!_existingRenderer) {
                DisposeHelper.Dispose(ref _renderer);
            }

            DisposeHelper.Dispose(ref _convertationThreadPool);
        }
    }
}