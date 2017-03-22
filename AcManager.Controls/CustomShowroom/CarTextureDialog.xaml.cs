using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using SlimDX.DXGI;

namespace AcManager.Controls.CustomShowroom {
    public class BakedShadowsRendererViewModel : INotifyPropertyChanged, IUserPresetable {
        public static readonly string PresetableKeyCategory = "Baked Shadows";

        private class SaveableData {
            public double From = 0d, To = 60d, Brightness = 220d, Gamma = 60d, Ambient = 0d;
            public int Iterations = 5000;
        }

        [CanBeNull]
        private readonly BaseRenderer _renderer;
        
        private readonly Kn5 _kn5;
        private readonly string _textureName;
        private readonly ISaveHelper _saveable;

        public BakedShadowsRendererViewModel([CanBeNull] BaseRenderer renderer, [NotNull] Kn5 kn5, [NotNull] string textureName) {
            _renderer = renderer;
            _kn5 = kn5;
            _textureName = textureName;
            _saveable = new SaveHelper<SaveableData>("_carTextureDialog", () => new SaveableData {
                From = From,
                To = To,
                Brightness = Brightness,
                Gamma = Gamma,
                Ambient = Ambient,
                Iterations = Iterations,
            }, o => {
                From = o.From;
                To = o.To;
                Brightness = o.Brightness;
                Gamma = o.Gamma;
                Ambient = o.Ambient;
                Iterations = o.Iterations;
            });

            _saveable.Initialize();
        }

        #region Properties
        private double _from = 0;

        public double From {
            get { return _from; }
            set {
                if (Equals(value, _from)) return;
                _from = value;
                OnPropertyChanged();
            }
        }

        private double _to = 60;

        public double To {
            get { return _to; }
            set {
                if (Equals(value, _to)) return;
                _to = value;
                OnPropertyChanged();
            }
        }

        private double _brightness = 220;

        public double Brightness {
            get { return _brightness; }
            set {
                if (Equals(value, _brightness)) return;
                _brightness = value;
                OnPropertyChanged();
            }
        }

        private double _gamma = 60;

        public double Gamma {
            get { return _gamma; }
            set {
                if (Equals(value, _gamma)) return;
                _gamma = value;
                OnPropertyChanged();
            }
        }

        private double _ambient = 0;

        public double Ambient {
            get { return _ambient; }
            set {
                if (Equals(value, _ambient)) return;
                _ambient = value;
                OnPropertyChanged();
            }
        }

        private int _iterations = 5000;

        public int Iterations {
            get { return _iterations; }
            set {
                if (Equals(value, _iterations)) return;
                _iterations = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Generating
        private const string KeyDimensions = "__BakedShadowsRendererViewModel.Dimensions";

        public async Task<bool> CalculateAo(int? size, string filename) {
            int width, height;
            switch (size) {
                case null:
                    var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                            ValuesStorage.GetString(KeyDimensions, ""), @"2048x2048");
                    if (string.IsNullOrWhiteSpace(result)) return false;

                    ValuesStorage.Set(KeyDimensions, result);

                    var match = Regex.Match(result, @"^\s*(\d+)(\s+|\s*\D\s*)(\d+)\s*$");
                    if (match.Success) {
                        width = FlexibleParser.ParseInt(match.Groups[1].Value);
                        height = FlexibleParser.ParseInt(match.Groups[2].Value);
                    } else {
                        int value;
                        if (FlexibleParser.TryParseInt(result, out value)) {
                            width = height = value;
                        } else {
                            NonfatalError.Notify(ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed,
                                    ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed_Commentary);
                            return false;
                        }
                    }
                    break;

                default:
                    width = height = size.Value;
                    break;
            }

            using (var waiting = WaitingDialog.Create("Rendering…")) {
                var cancellation = waiting.CancellationToken;
                var progress = (IProgress<double>)waiting;

                await Task.Run(() => {
                    using (var renderer = new BakedShadowsRenderer(_kn5) {
                        ΘFrom = (float)From,
                        ΘTo = (float)To,
                        Iterations = Iterations,
                        SkyBrightnessLevel = (float)Brightness / 100f,
                        Gamma = (float)Gamma / 100f,
                        Ambient = (float)Ambient / 100f,
                    }) {
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, _textureName, progress, cancellation);
                    }
                });

                if (cancellation.IsCancellationRequested) return false;
            }

            return true;
        }

        private static string GetShortChecksum(string s) {
            var f = s.Length / 2;
            var h = s.Substring(f, s.Length - f).GetHashCode();
            var g = s.Substring(0, f).GetHashCode();
            var r = new byte[8];
            Array.Copy(BitConverter.GetBytes(h), 0, r, 0, 4);
            Array.Copy(BitConverter.GetBytes(g), 0, r, 4, 4);
            return Convert.ToBase64String(r).TrimEnd('=');
        }

        private AsyncCommand<string> _calculateAoCommand;

        public AsyncCommand<string> CalculateAoCommand => _calculateAoCommand ?? (_calculateAoCommand = new AsyncCommand<string>(async o => {
            var filename = FilesStorage.Instance.GetTemporaryFilename(
                    $"{FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(_textureName))} AO.png");
            if (!await CalculateAo(FlexibleParser.TryParseInt(o), filename)) return;

            var uniquePostfix = GetShortChecksum(_kn5.OriginalFilename);
            var originalTexture = FilesStorage.Instance.GetTemporaryFilename(
                    $"{FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(_textureName))} Original ({uniquePostfix}).tmp");
            Logging.Debug($"Postfix: {uniquePostfix}");

            if (File.Exists(originalTexture)) {
                var sw = Stopwatch.StartNew();
                var image = BetterImage.LoadBitmapSource(originalTexture);

                if (image.BitmapSource != null) {
                    Logging.Debug($"Cached texture loaded: {sw.Elapsed.TotalMilliseconds:F1} ms");
                    new ImageViewer(new object[] { filename, image }) {
                        Model = {
                            Saveable = true,
                            SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                            SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename)
                        }
                    }.ShowDialog();
                    return;
                }
            }

            byte[] data;
            if (_renderer != null && _kn5.TexturesData.TryGetValue(_textureName, out data)) {
                var sw = Stopwatch.StartNew();
                var image = CarTextureDialog.LoadImageUsingDirectX(_renderer, data);

                if (image != null) {
                    image.Image.SaveAsPng(originalTexture);
                    Logging.Debug($"Cached texture saved: {sw.Elapsed.TotalMilliseconds:F1} ms");
                    new ImageViewer(new object[] { filename, image.Image }) {
                        Model = {
                            Saveable = true,
                            SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                            SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename)
                        }
                    }.ShowDialog();
                    return;
                }
            }

            new ImageViewer(filename) {
                Model = {
                    Saveable = true,
                    SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                    SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename)
                }
            }.ShowDialog();
        }));
        #endregion

        #region Presetable
        public bool CanBeSaved => true;

        public string PresetableCategory => PresetableKeyCategory;

        public string PresetableKey => PresetableKeyCategory;

        public string DefaultPreset => null;

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (_saveable.SaveLater()) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion
    }

    public partial class CarTextureDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public CarTextureDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
            DataContext = new ViewModel(renderer, activeSkin, kn5, textureName);
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        public class ViewModel : NotifyPropertyChanged {
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;
            private readonly Kn5 _kn5;

            public BakedShadowsRendererViewModel BakedShadows { get; }

            public string TextureName { get; }

            public string TextureFormat { get; }

            private string _textureFormatDescription;

            public string TextureFormatDescription {
                get { return _textureFormatDescription; }
                set {
                    if (Equals(value, _textureFormatDescription)) return;
                    _textureFormatDescription = value;
                    OnPropertyChanged();
                }
            }

            private string _textureDimensions;

            public string TextureDimensions {
                get { return _textureDimensions; }
                set {
                    if (value == _textureDimensions) return;
                    _textureDimensions = value;
                    OnPropertyChanged();
                }
            }

            public byte[] Data { get; }

            private bool _loading;

            public bool Loading {
                get { return _loading; }
                set {
                    if (Equals(value, _loading)) return;
                    _loading = value;
                    OnPropertyChanged();
                }
            }

            private BitmapSource _previewImage;

            public BitmapSource PreviewImage {
                get { return _previewImage; }
                set {
                    if (Equals(value, _previewImage)) return;
                    _previewImage = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel([CanBeNull] BaseRenderer renderer, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                TextureName = textureName;

                var format = Regex.Match(textureName, @"(?<=\.)([a-zA-Z]{3,4})$").Value;
                TextureFormat = string.IsNullOrWhiteSpace(format) ? null : format.ToUpperInvariant();

                byte[] data;
                Data = kn5.TexturesData.TryGetValue(textureName, out data) ? data : null;

                BakedShadows = new BakedShadowsRendererViewModel(renderer, _kn5, TextureName);
            }

            public async void OnLoaded() {
                Loading = true;

                var loaded = _renderer == null ? await LoadImageUsingMagickNet(Data) : LoadImageUsingDirectX(_renderer, Data);
                PreviewImage = loaded?.Image;
                TextureFormatDescription = loaded?.FormatDescription;
                TextureDimensions = PreviewImage == null ? null : $"{PreviewImage.PixelWidth}×{PreviewImage.PixelHeight}";
                Loading = false;
            }

            private const string KeyDimensions = "__CarTextureDialog.Dimensions";

            private CommandBase _uvCommand;

            public ICommand UvCommand => _uvCommand ?? (_uvCommand = new AsyncCommand<string>(async o => {
                var filename = FilesStorage.Instance.GetTemporaryFilename(
                        FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(TextureName)) + " UV.png");
                
                int width, height;
                switch (o) {
                    case "custom":
                        var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                                ValuesStorage.GetString(KeyDimensions, ""), @"2048x2048");
                        if (string.IsNullOrWhiteSpace(result)) return;

                        ValuesStorage.Set(KeyDimensions, result);

                        var match = Regex.Match(result, @"^\s*(\d+)(\s+|\s*\D\s*)(\d+)\s*$");
                        if (match.Success) {
                            width = FlexibleParser.ParseInt(match.Groups[1].Value);
                            height = FlexibleParser.ParseInt(match.Groups[2].Value);
                        } else {
                            int value;
                            if (FlexibleParser.TryParseInt(result, out value)) {
                                width = height = value;
                            } else {
                                NonfatalError.Notify(ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed,
                                        ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed_Commentary);
                                return;
                            }
                        }
                        break;

                    default:
                        width = height = FlexibleParser.TryParseInt(o) ?? 2048;
                        break;
                }

                await Task.Run(() => {
                    using (var renderer = new UvRenderer(_kn5)) {
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, TextureName);
                    }
                });

                new ImageViewer(filename) {
                    Model = {
                        Saveable = true,
                        SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                        SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename)
                    }
                }.ShowDialog();
            }));

            private CommandBase _exportCommand;

            public ICommand ExportCommand => _exportCommand ?? (_exportCommand = new AsyncCommand(async () => {
                var dialog = new SaveFileDialog {
                    InitialDirectory = _activeSkin?.Location ?? Path.GetDirectoryName(_kn5.OriginalFilename),
                    Filter = string.Format(@"Textures (*.{0})|*.{0}", TextureFormat.ToLower()),
                    DefaultExt = TextureFormat.ToLower(),
                    FileName = TextureName
                };

                if (dialog.ShowDialog() != true) return;

                try {
                    await Task.Run(() => File.WriteAllBytes(dialog.FileName, Data));
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.CustomShowroom_CannotExport, e);
                }
            }, () => Data != null));
        }

        internal class LoadedImage {
            public BitmapSource Image;
            public string FormatDescription;
        }

        [CanBeNull]
        internal static LoadedImage LoadImageUsingDirectX(BaseRenderer renderer, byte[] imageData) {
            if (imageData == null || imageData.Length == 0) return null;

            try {
                Format format;
                var pngData = TextureReader.ToPng(renderer.DeviceContextHolder, imageData, true, out format);

                var image = new BitmapImage();
                using (var stream = new MemoryStream(pngData) {
                    Position = 0
                }) {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();

                return new LoadedImage {
                    Image = image,
                    FormatDescription = format.ToString()
                };
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull]
        internal static async Task<LoadedImage> LoadImageUsingMagickNet(byte[] imageData) {
            if (imageData == null || imageData.Length == 0) return null;

            try {
                string formatDescription = null;
                if (ImageUtils.IsMagickSupported) {
                    var data = imageData;
                    imageData = await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(data, true, out formatDescription));
                }

                var image = new BitmapImage();
                using (var stream = new MemoryStream(imageData) {
                    Position = 0
                }) {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();

                return new LoadedImage {
                    Image = image,
                    FormatDescription = formatDescription
                };
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        private bool _loaded;
        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            Model.OnLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
        }

        private void Preview_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            new ImageViewer(Model.PreviewImage) { Owner = null }.ShowDialog();
        }
    }
}
