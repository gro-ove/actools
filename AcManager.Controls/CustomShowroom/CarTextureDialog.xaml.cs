using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Controls.CustomShowroom {
    public partial class CarTextureDialog {
        private CarTextureDialogViewModel Model => (CarTextureDialogViewModel)DataContext;

        public CarTextureDialog([CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
            DataContext = new CarTextureDialogViewModel(activeSkin, kn5, textureName);
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        public class CarTextureDialogViewModel : NotifyPropertyChanged {
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

            private BitmapImage _previewImage;

            public BitmapImage PreviewImage {
                get { return _previewImage; }
                set {
                    if (Equals(value, _previewImage)) return;
                    _previewImage = value;
                    OnPropertyChanged();
                }
            }

            public CarTextureDialogViewModel([CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, [NotNull] string textureName) {
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

                var loaded = await LoadImage(Data);
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
                public BitmapImage Image;
                public string FormatDescription;
            }

            [ItemCanBeNull]
            private static async Task<LoadedImage> LoadImage(byte[] imageData) {
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
                    Logging.Warning("Can’t show texture preview: " + e);
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
