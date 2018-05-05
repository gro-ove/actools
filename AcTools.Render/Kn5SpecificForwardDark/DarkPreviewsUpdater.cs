using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX;
using ThreadPool = AcTools.Render.Base.Utils.ThreadPool;

namespace AcTools.Render.Kn5SpecificForwardDark {
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

                existingRenderer.LoadCarLights = true;
                existingRenderer.LoadShowroomLights = true;
                existingRenderer.Lights = existingRenderer.Lights.Where(x => !x.Tag.IsCarTag && !x.Tag.IsShowroomTag).ToArray();
            }
        }

        public void SetOptions([NotNull] DarkPreviewsOptions options) {
            _options = options;

            if (_renderer != null) {
                SetRendererOptions(_acRoot, _renderer, options);
                SetRendererCarOptions(_renderer, options);
                UpdateCamera();
            }
        }

        private CarDescription GetCarDescription([NotNull] string carId, [CanBeNull] DataWrapper carData) {
            var carDirectory = AcPaths.GetCarDirectory(_acRoot, carId);
            var carKn5 = carData == null ? AcPaths.GetMainCarFilename(carDirectory) : AcPaths.GetMainCarFilename(carDirectory, carData);
            return new CarDescription(carKn5, carDirectory);
        }

        private static Vector3 ToVector3([NotNull] double[] v) {
            return new Vector3((float)v[0], (float)v[1], (float)v[2]);
        }

        private void UpdateCamera() {
            _renderer.SetCamera(
                    ToVector3(_options.CameraPosition), ToVector3(_options.CameraLookAt),
                    (float)(MathF.PI / 180f * _options.CameraFov),
                    (float)(MathF.PI / 180f * _options.CameraTilt));

            if (_options.AlignCar) {
                _renderer.AlignCar();
            }

            _renderer.AlignCamera(_options.AlignCameraHorizontally, (float)_options.AlignCameraHorizontallyOffset,
                    _options.AlignCameraHorizontallyOffsetRelative,
                    _options.AlignCameraVertically, (float)_options.AlignCameraVerticallyOffset, _options.AlignCameraVerticallyOffsetRelative);
        }

        [CanBeNull]
        private static string GetShowroomKn5([NotNull] string acRoot, [CanBeNull] string showroomValue) {
            try {
                if (showroomValue == null || File.Exists(showroomValue)) return showroomValue;
                var kn5 = Path.Combine(AcPaths.GetShowroomDirectory(acRoot, showroomValue), $"{showroomValue}.kn5");
                return File.Exists(kn5) ? kn5 : null;
            } catch {
                // In case showroomValue is not a valid file path or something
                return null;
            }
        }

        private static DarkKn5ObjectRenderer CreateRenderer([NotNull] string acRoot, [NotNull] DarkPreviewsOptions options,
                [CanBeNull] CarDescription initialCar, [CanBeNull] string initialSkinId) {
            var renderer = new DarkKn5ObjectRenderer(initialCar, GetShowroomKn5(acRoot, options.Showroom)) {
                LoadCarLights = true,
                LoadShowroomLights = true
            };

            SetRendererOptions(acRoot, renderer, options);
            renderer.SelectSkin(initialSkinId);
            renderer.Initialize();
            SetRendererCarOptions(renderer, options);
            return renderer;
        }

        private static void SetRendererOptions([NotNull] string acRoot, [NotNull] DarkKn5ObjectRenderer renderer, [NotNull] DarkPreviewsOptions options) {
            var showroomKn5 = GetShowroomKn5(acRoot, options.Showroom);
            if (showroomKn5 != renderer.CurrentShowroomKn5) {
                renderer.SetShowroom(showroomKn5);
            }

            // Obvious fixed settings
            renderer.AutoRotate = false;
            renderer.AutoAdjustTarget = false;
            renderer.AsyncTexturesLoading = false;

            // UI should be off!
            renderer.VisibleUi = false;

            // Size-related options
            renderer.Width = (int)(options.PreviewWidth * options.SsaaMultiplier);
            renderer.Height = (int)(options.PreviewHeight * options.SsaaMultiplier);
            renderer.ResolutionMultiplier = 1d;
            renderer.UseFxaa = options.UseFxaa;
            renderer.UseSmaa = options.UseSmaa;
            renderer.UseMsaa = options.UseMsaa;
            renderer.MsaaSampleCount = options.MsaaSampleCount;

            // Switches
            renderer.WireframeMode = options.WireframeMode ? WireframeMode.LinesOnly : WireframeMode.Disabled;
            renderer.MeshDebug = options.MeshDebugMode;

            // Flat mirror
            renderer.AnyGround = options.AnyGround;
            renderer.FlatMirror = options.FlatMirror;
            renderer.FlatMirrorBlurred = options.FlatMirrorBlurred;
            renderer.FlatMirrorBlurMuiltiplier = (float)options.FlatMirrorBlurMuiltiplier;
            renderer.FlatMirrorReflectiveness = (float)options.FlatMirrorReflectiveness;
            renderer.FlatMirrorReflectedLight = options.FlatMirrorReflectedLight;

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
            renderer.ReflectionsWithMultipleLights = options.ReflectionsWithMultipleLights;

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

            // Custom reflections
            renderer.UseCustomReflectionCubemap = options.UseCustomReflectionCubemap;
            renderer.CustomReflectionBrightness = (float)options.CustomReflectionBrightness;
            renderer.CustomReflectionCubemap = options.CustomReflectionCubemapData;

            // Color
            renderer.ToneMapping = options.ToneMapping;
            renderer.UseDither = options.UseDither;
            renderer.UseColorGrading = options.UseColorGrading;
            renderer.ToneExposure = (float)options.ToneExposure;
            renderer.ToneGamma = (float)options.ToneGamma;
            renderer.ToneWhitePoint = (float)options.ToneWhitePoint;
            renderer.ColorGradingData = options.ColorGradingData;

            // Extra
            renderer.BloomRadiusMultiplier = (float)(options.SsaaMultiplier * options.BloomRadiusMultiplier);
            renderer.MaterialsReflectiveness = (float)options.MaterialsReflectiveness;
            renderer.CarShadowsOpacity = (float)options.CarShadowsOpacity;
            renderer.PcssLightScale = (float)options.PcssLightScale;
            renderer.PcssSceneScale = (float)options.PcssSceneScale;
            renderer.AoOpacity = (float)options.AoOpacity;
            renderer.AoRadius = (float)options.AoRadius;

            // DOF
            renderer.UseDof = options.UseDof;
            renderer.DofFocusPlane = (float)options.DofFocusPlane;
            renderer.DofScale = (float)options.DofScale;
            renderer.UseAccumulationDof = options.UseAccumulationDof;
            renderer.AccumulationDofBokeh = options.AccumulationDofBokeh;
            renderer.AccumulationDofIterations = options.AccumulationDofIterations;
            renderer.AccumulationDofApertureSize = (float)options.AccumulationDofApertureSize;

            // Lights
            renderer.DeserializeLights(DarkLightTag.Extra, JArray.Parse(options.SerializedLights ?? @"[]").OfType<JObject>());
            renderer.TryToGuessCarLights = options.TryToGuessCarLights;
            renderer.LoadCarLights = options.LoadCarLights;
            renderer.LoadShowroomLights = options.LoadShowroomLights;
        }

        private static void SetRendererCarOptions([NotNull] DarkKn5ObjectRenderer renderer, [NotNull] DarkPreviewsOptions options) {
            var car = renderer.CarNode;
            if (car != null) {
                car.HeadlightsEnabled = options.HeadlightsEnabled;
                car.BrakeLightsEnabled = options.BrakeLightsEnabled;
                car.SteerDeg = (float)options.SteerDeg;
                car.LeftDoorOpen = options.LeftDoorOpen;
                car.RightDoorOpen = options.RightDoorOpen;
                car.IsDriverVisible = options.ShowDriver;

                if (options.ExtraActiveAnimations != null) {
                    foreach (var animation in options.ExtraActiveAnimations) {
                        var wing = car.Wings.OfType<Kn5RenderableCar.AnimationEntryBase>().Concat(car.Extras)
                                      .FirstOrDefault(x => x.DisplayName == animation);
                        if (wing != null) {
                            wing.IsActive = true;
                        }
                    }
                }
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

        private void ProcessConvertation([NotNull] Action action, [NotNull] Action disposal) {
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
                    AcToolsLogging.Write(e);
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

        private void ShotInner([NotNull] string carId, [NotNull] string skinId, [CanBeNull] string destination,
                [CanBeNull] ImageUtils.ImageInformation information, [CanBeNull] Action callback) {
            if (destination == null) {
                destination = Path.Combine(AcPaths.GetCarSkinDirectory(_acRoot, carId, skinId), _options.PreviewName);
            }

            var shotStream = new MemoryStream(_approximateSize ?? 100000);

            _renderer.Shot(_renderer.Width, _renderer.Height, _options.SoftwareDownsize ? 1d : 1d / _options.SsaaMultiplier, 1d, shotStream,
                    RendererShotFormat.Png);
            if (!_approximateSize.HasValue || _approximateSize < shotStream.Position) {
                _approximateSize = (int)(shotStream.Position * 1.2);
            }

            shotStream.Position = 0;
            ProcessConvertation(() => {
                using (var stream = File.Open(destination, FileMode.Create, FileAccess.ReadWrite)) {
                    ImageUtils.Convert(shotStream, stream,
                            _options.SoftwareDownsize ? new Size(_options.PreviewWidth, _options.PreviewHeight) : (Size?)null, exif: information,
                            format: destination.EndsWith(".png") ? ImageFormat.Png : ImageFormat.Jpeg);
                    callback?.Invoke();
                }
            }, DisposeCallback(shotStream));
        }

        private Task ShotInnerAsync([NotNull] string carId, [NotNull] string skinId, [CanBeNull] string destination,
                [CanBeNull] ImageUtils.ImageInformation information, [CanBeNull] Action callback) {
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
        public void Shot([NotNull] string carId, [NotNull] string skinId, string destination = null, DataWrapper carData = null, ImageUtils.ImageInformation information = null,
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
        public async Task ShotAsync([NotNull] string carId, [NotNull] string skinId, string destination = null, DataWrapper carData = null,
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