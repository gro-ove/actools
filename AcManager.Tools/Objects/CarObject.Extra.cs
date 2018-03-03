using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Effects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

#pragma warning disable 649

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject : ICupSupportedObject {
        private LazierThis<double?> _steerLock;
        public double? SteerLock => _steerLock.Get(() => AcdData?.GetIniFile("car.ini")["CONTROLS"].GetDoubleNullable("STEER_LOCK") * 2d);

        string ICupSupportedObject.InstalledVersion => Version;
        public CupContentType CupContentType => CupContentType.Car;
        public bool IsCupUpdateAvailable => CupClient.Instance.ContainsAnUpdate(CupContentType.Car, Id, Version);
        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance.GetInformation(CupContentType.Car, Id);

        protected override void OnVersionChanged() {
            OnPropertyChanged(nameof(ICupSupportedObject.InstalledVersion));
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        public void SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            Save();
        }

        [CanBeNull]
        private JObject ReadCmTextures() {
            if (!File.Exists(CmTexturesFilename)) return null;

            try {
                return JObject.Parse(File.ReadAllText(CmTexturesFilename));
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load car’s textures description", e);
                return null;
            }
        }

        [CanBeNull]
        private ShaderEffect GetEffect([CanBeNull] string textureEffect) {
            switch (textureEffect?.ToLowerInvariant()) {
                case "grayscale":
                    return new GrayscaleEffect();
                case "invert":
                    return new InvertEffect();
                case "invertkeepcolor":
                case "invertkeepcolors":
                    return new InvertKeepColorEffect();
                default:
                    return null;
            }
        }

        private byte[] PrepareTexture(string filename, string textureEffect) {
            var effect = GetEffect(textureEffect);
            if (effect == null) return File.ReadAllBytes(filename);

            var image = BetterImage.LoadBitmapSource(filename);
            var size = new Size(image.Width, image.Height);

            var result = new Image {
                Width = image.Width,
                Height = image.Height,
                Source = image.BitmapSource,
                Effect = effect,
            };

            result.Measure(size);
            result.Arrange(new Rect(size));
            result.ApplyTemplate();
            result.UpdateLayout();

            var bmp = new RenderTargetBitmap(image.Width, image.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(result);
            return bmp.ToBytes();
        }

        private void SetTrackOutlineTexture([CanBeNull] string filename, [NotNull] string textureName, [CanBeNull] string textureEffect) {
            try {
                var data = Lazier.Create(() => {
                    return filename == null || !File.Exists(filename) ? null : ActionExtension.InvokeInMainThread(() => PrepareTexture(filename, textureEffect));
                });

                string alreadySaved = null;
                foreach (var skinObject in EnabledOnlySkins.ToList()) {
                    if (!Directory.Exists(skinObject.Location)) return;
                    var location = Path.Combine(skinObject.Location, textureName);

                    if (data.Value != null) {
                        if (alreadySaved != null) {
                            FileUtils.HardLinkOrCopy(alreadySaved, location, true);
                        } else {
                            try {
                                File.WriteAllBytes(location, data.Value);
                                alreadySaved = location;
                            } catch (Exception e) {
                                Logging.Error(e);
                            }
                        }
                    } else {
                        FileUtils.TryToDelete(location);
                    }
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t set track outline texture", e);
            }
        }

        private void PrepareTrackOutlineTexture([NotNull] JToken cmTextures, [CanBeNull] TrackObjectBase track) {
            var outline = cmTextures.GetStringValueOnly("trackOutline");
            if (outline != null) {
                SetTrackOutlineTexture(track?.OutlineImage, outline, cmTextures.GetStringValueOnly("trackOutlineEffect"));
            }
        }

        public void PrepareRaceTextures([CanBeNull] TrackObjectBase track) {
            var d = ReadCmTextures();
            if (d == null) return;

            PrepareTrackOutlineTexture(d, track);
        }
    }
}
