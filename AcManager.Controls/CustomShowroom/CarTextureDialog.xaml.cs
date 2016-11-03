using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;
using SlimDX.DXGI;

namespace AcManager.Controls.CustomShowroom {
    public partial class CarTextureDialog {
        private CarTextureDialogViewModel Model => (CarTextureDialogViewModel)DataContext;

        public CarTextureDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
            DataContext = new CarTextureDialogViewModel(renderer, activeSkin, kn5, textureName);
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        class SharedBitmapSource : BitmapSource, IDisposable {
            #region Public Properties

            /// <summary>
            /// I made it public so u can reuse it and get the best our of both namespaces
            /// </summary>
            public Bitmap Bitmap { get; private set; }

            public override double DpiX { get { return Bitmap.HorizontalResolution; } }

            public override double DpiY { get { return Bitmap.VerticalResolution; } }

            public override int PixelHeight { get { return Bitmap.Height; } }

            public override int PixelWidth { get { return Bitmap.Width; } }

            public override System.Windows.Media.PixelFormat Format { get { return ConvertPixelFormat(Bitmap.PixelFormat); } }

            public override BitmapPalette Palette { get { return null; } }

            #endregion

            #region Constructor/Destructor

            public SharedBitmapSource(int width, int height, System.Drawing.Imaging.PixelFormat sourceFormat)
                : this(new Bitmap(width, height, sourceFormat)) { }

            public SharedBitmapSource(Bitmap bitmap) {
                Bitmap = bitmap;
            }

            // Use C# destructor syntax for finalization code.
            ~SharedBitmapSource() {
                // Simply call Dispose(false).
                Dispose(false);
            }

            #endregion

            #region Overrides

            public override void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset) {
                BitmapData sourceData = Bitmap.LockBits(
                new Rectangle(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height),
                ImageLockMode.ReadOnly,
                Bitmap.PixelFormat);

                var length = sourceData.Stride * sourceData.Height;

                if (pixels is byte[]) {
                    var bytes = pixels as byte[];
                    Marshal.Copy(sourceData.Scan0, bytes, 0, length);
                }

                Bitmap.UnlockBits(sourceData);
            }

            protected override Freezable CreateInstanceCore() {
                return (Freezable)Activator.CreateInstance(GetType());
            }

            #endregion

            #region Public Methods

            public BitmapSource Resize(int newWidth, int newHeight) {
                Image newImage = new Bitmap(newWidth, newHeight);
                using (Graphics graphicsHandle = Graphics.FromImage(newImage)) {
                    graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphicsHandle.DrawImage(Bitmap, 0, 0, newWidth, newHeight);
                }
                return new SharedBitmapSource(newImage as Bitmap);
            }

            public new BitmapSource Clone() {
                return new SharedBitmapSource(new Bitmap(Bitmap));
            }

            //Implement IDisposable.
            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            #endregion

            #region Protected/Private Methods

            private static System.Windows.Media.PixelFormat ConvertPixelFormat(System.Drawing.Imaging.PixelFormat sourceFormat) {
                switch (sourceFormat) {
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        return PixelFormats.Bgr24;

                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                        return PixelFormats.Pbgra32;

                    case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                        return PixelFormats.Bgr32;

                }
                return new System.Windows.Media.PixelFormat();
            }

            private bool _disposed = false;

            protected virtual void Dispose(bool disposing) {
                if (!_disposed) {
                    if (disposing) {
                        // Free other state (managed objects).
                    }
                    // Free your own state (unmanaged objects).
                    // Set large fields to null.
                    _disposed = true;
                }
            }

            #endregion
        }

        public class CarTextureDialogViewModel : NotifyPropertyChanged {
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;
            private readonly Kn5 _kn5;

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

            public CarTextureDialogViewModel([CanBeNull] BaseRenderer renderer, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                TextureName = textureName;

                var format = Regex.Match(textureName, @"(?<=\.)([a-zA-Z]{3,4})$").Value;
                TextureFormat = string.IsNullOrWhiteSpace(format) ? null : format.ToUpperInvariant();

                byte[] data;
                Data = kn5.TexturesData.TryGetValue(textureName, out data) ? data : null;
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

            private class LoadedImage {
                public BitmapSource Image;
                public string FormatDescription;
            }

            [ItemCanBeNull]
            private static LoadedImage LoadImageUsingDirectX(BaseRenderer renderer, byte[] imageData) {
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
            private static async Task<LoadedImage> LoadImageUsingMagickNet(byte[] imageData) {
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
        }

        private bool _loaded;
        private void CarTextureDialog_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            Model.OnLoaded();
        }

        private void CarTextureDialog_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
        }

        private void Preview_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            new ImageViewer(Model.PreviewImage).ShowDialog();
        }
    }
}
