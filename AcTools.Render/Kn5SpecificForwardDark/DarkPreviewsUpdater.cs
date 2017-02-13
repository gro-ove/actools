using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkPreviewsOptions {
        public double SsaaMultipler = 4d;
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

        private Task ShotInner(string carId, string skinId, string destination) {
            if (destination == null) {
                destination = Path.Combine(FileUtils.GetCarSkinDirectory(_acRoot, carId, skinId), "preview.jpg");
            }

            return Task.Run(() => {
                _renderer.Shot(1).HighQualityResize(new Size(CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight)).Save(destination);
            });
        }

        public async Task Shot(string carId, string skinId, string destination = null, DataWrapper carData = null) {
            if (_carId != carId) {
                var carDirectory = FileUtils.GetCarDirectory(_acRoot, carId);
                var carKn5 = carData == null ? FileUtils.GetMainCarFilename(carDirectory) : FileUtils.GetMainCarFilename(carDirectory, carData);

                if (_renderer == null) {
                    _renderer = await Task.Run(() => {
                        var renderer = new DarkKn5ObjectRenderer(new CarDescription(carKn5, carDirectory)) {
                            Width = (int)(CommonAcConsts.PreviewWidth * _options.SsaaMultipler),
                            Height = (int)(CommonAcConsts.PreviewHeight * _options.SsaaMultipler),
                            BackgroundColor = Color.Black,
                            LightColor = Color.FromArgb(0xffffff),
                            AmbientUp = Color.FromArgb(0xb4b496),
                            AmbientDown = Color.FromArgb(0x96b4b4),
                            AmbientBrightness = 2.0f,
                            LightBrightness = 1.5f,
                            FlatMirror = false,
                            UseMsaa = false,
                            UseFxaa = false,
                            AutoRotate = false,
                            AsyncTexturesLoading = false
                        };

                        renderer.SetCamera(new Vector3(3.194f, 0.342f, 13.049f), new Vector3(2.945f, 0.384f, 12.082f), MathF.PI / 180f * 9f);
                        renderer.Initialize();
                        return renderer;
                    }).ConfigureAwait(false);
                } else {
                    _renderer.SetCar(new CarDescription(carKn5, carDirectory), skinId);
                }

                _carId = carId;
            } else {
                _renderer.SelectSkin(skinId);
            }

            await ShotInner(carId, skinId, destination).ConfigureAwait(false);
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _renderer);
        }
    }
}