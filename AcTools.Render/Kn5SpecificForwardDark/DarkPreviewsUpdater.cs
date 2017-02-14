using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkPreviewsOptions {
        public string PreviewName = "preview.jpg";

        public double SsaaMultipler = 4d;
        public int PreviewWidth = CommonAcConsts.PreviewWidth;
        public int PreviewHeight = CommonAcConsts.PreviewHeight;
        public bool UseFxaa = false;
        public bool UseMsaa = false;
        public int MsaaSampleCount = 4;
        public double BloomRadiusMultipler = 1d;
        public bool HardwareDownscale = true;

        public bool WireframeMode = false;
        public bool MeshDebugMode = false;
        public bool SuspensionDebugMode = false;

        public bool HeadlightsEnabled = false;
        public bool BrakeLightsEnabled = false;
        public double SteerAngle = 0d;

        public double[] CameraPosition = { 3.194, 0.342, 13.049 };
        public double[] CameraLookAt = { 2.945, 0.384, 12.082 };
        public double CameraFov = 9d;

        public bool FlatMirror = false;
        public Color BackgroundColor = Color.Black;
        public Color LightColor = Color.FromArgb(0xffffff);
        public Color AmbientUp = Color.FromArgb(0xb4b496);
        public Color AmbientDown = Color.FromArgb(0x96b4b4);

        public double AmbientBrightness = 2d;
        public double LightBrightness = 1.5;
    }

    public class DarkPreviewsUpdater : IDisposable {
        private readonly string _acRoot;
        private readonly DarkPreviewsOptions _options;

        private DarkKn5ObjectRenderer _renderer;
        private string _carId;

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

        private static DarkKn5ObjectRenderer CreateRenderer(DarkPreviewsOptions options, CarDescription initialCar) {
            var renderer = new DarkKn5ObjectRenderer(initialCar) {
                // Obvious fixed settings
                AutoRotate = false,
                AsyncTexturesLoading = false,

                // Size-related options
                Width = (int)(options.PreviewWidth * options.SsaaMultipler),
                Height = (int)(options.PreviewHeight * options.SsaaMultipler),
                UseFxaa = options.UseFxaa,
                UseMsaa = options.UseMsaa,
                MsaaSampleCount = options.MsaaSampleCount,
                KeepFxaaWhileShooting = true,
                BloomRadiusMultipler = (float)(options.SsaaMultipler * options.BloomRadiusMultipler),

                // Switches
                FlatMirror = options.FlatMirror,
                ShowWireframe = options.WireframeMode,
                MeshDebug = options.MeshDebugMode,
                SuspensionDebug = options.SuspensionDebugMode,

                // Colors
                BackgroundColor = options.BackgroundColor,
                LightColor = options.LightColor,
                AmbientUp = options.AmbientUp,
                AmbientDown = options.AmbientDown,

                // Brightnesses
                AmbientBrightness = (float)options.AmbientBrightness,
                LightBrightness = (float)options.LightBrightness,
            };

            renderer.SetCamera(ToVector3(options.CameraPosition), ToVector3(options.CameraLookAt), (float)(MathF.PI / 180f * options.CameraFov));
            renderer.Initialize();

            var car = renderer.CarNode;
            if (car != null) {
                car.LightsEnabled = options.HeadlightsEnabled;
                car.BrakeLightsEnabled = options.BrakeLightsEnabled;
                car.SteerDeg = (float)options.SteerAngle;
            }

            return renderer;
        }

        private void ShotInner(string carId, string skinId, string destination) {
            if (destination == null) {
                destination = Path.Combine(FileUtils.GetCarSkinDirectory(_acRoot, carId, skinId), _options.PreviewName);
            }

            if (_options.HardwareDownscale) {
                /*using (var stream = File.Open(destination, FileMode.Create, FileAccess.Write)) {
                    _renderer.Shot(1d, 1d / _options.SsaaMultipler, stream);
                }*/
                using (var shot = new MemoryStream()){
                    _renderer.Shot(1d, 1d / _options.SsaaMultipler, shot);
                    shot.Position = 0;

                    using (var image = Image.FromStream(shot)) {
                        var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                        var parameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, 1000L) } };
                        image.Save(destination, encoder, parameters);
                    }
                }
            } else {
                using (var shot = _renderer.Shot(1d, 1d))
                using (var resized = shot.HighQualityResize(new Size(_options.PreviewWidth, _options.PreviewHeight))) {
                    resized.Save(destination);
                }
            }
        }

        private Task ShotInnerAsync(string carId, string skinId, string destination) {
            return Task.Run(() => ShotInner(carId, skinId, destination));
        }

        public void Shot(string carId, string skinId, string destination = null, DataWrapper carData = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = CreateRenderer(_options, GetCarDescription(carId, carData));
                } else {
                    _renderer.SetCar(GetCarDescription(carId, carData), skinId);
                }
                _carId = carId;
            } else {
                _renderer.SelectSkin(skinId);
            }

            ShotInner(carId, skinId, destination);
        }

        public async Task ShotAsync(string carId, string skinId, string destination = null, DataWrapper carData = null) {
            if (_carId != carId) {
                if (_renderer == null) {
                    _renderer = await Task.Run(() => CreateRenderer(_options, GetCarDescription(carId, carData))).ConfigureAwait(false);
                } else {
                    _renderer.SetCar(GetCarDescription(carId, carData), skinId);
                }
                _carId = carId;
            } else {
                _renderer.SelectSkin(skinId);
            }

            await ShotInnerAsync(carId, skinId, destination).ConfigureAwait(false);
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _renderer);
        }
    }
}