using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;
using SlimDX.DXGI;

namespace AcManager.CustomShowroom {
    public class BakedShadowsRendererViewModel : INotifyPropertyChanged, IUserPresetable {
        public static readonly string PresetableKeyCategory = "Baked Shadows";

        private class SaveableData {
            public double From = 0d, To = 60d, Brightness = 220d, Gamma = 60d, Ambient, ShadowBias;
            public int Iterations = 5000;
        }

        [CanBeNull]
        private readonly BaseRenderer _renderer;
        
        private readonly Kn5 _kn5;
        private readonly string _textureName;
        private readonly ISaveHelper _saveable;

        public Size? Size { private get; set; }

        [CanBeNull]
        private readonly CarObject _car;

        public BakedShadowsRendererViewModel([CanBeNull] BaseRenderer renderer, [NotNull] Kn5 kn5, [NotNull] string textureName,
                [CanBeNull] CarObject car) {
            _renderer = renderer;
            _kn5 = kn5;
            _textureName = textureName;
            _car = car;
            _saveable = new SaveHelper<SaveableData>("_carTextureDialog", () => new SaveableData {
                From = From,
                To = To,
                Brightness = Brightness,
                Gamma = Gamma,
                Ambient = Ambient,
                Iterations = Iterations,
                ShadowBias = ShadowBias,
            }, o => {
                From = o.From;
                To = o.To;
                Brightness = o.Brightness;
                Gamma = o.Gamma;
                Ambient = o.Ambient;
                Iterations = o.Iterations;
                ShadowBias = o.ShadowBias;
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

        private double _shadowBias;

        public double ShadowBias {
            get { return _shadowBias; }
            set {
                if (Equals(value, _shadowBias)) return;
                _shadowBias = value;
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

        public async Task<Size?> CalculateAo(int? size, string filename, [CanBeNull] CarObject car) {
            int width, height;
            switch (size) {
                case null:
                    var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                            ValuesStorage.GetString(KeyDimensions, Size.HasValue ? $"{Size?.Width}x{Size?.Height}" : ""), @"2048x2048");
                    if (string.IsNullOrWhiteSpace(result)) return null;

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
                            return null;
                        }
                    }
                    break;

                case -1:
                    width = (int)(Size?.Width ?? 1024);
                    height = (int)(Size?.Height ?? 1024);
                    break;

                default:
                    width = height = size.Value;
                    break;
            }

            using (var waiting = WaitingDialog.Create("Rendering…")) {
                var cancellation = waiting.CancellationToken;
                var progress = (IProgress<double>)waiting;

                await Task.Run(() => {
                    using (var renderer = new BakedShadowsRenderer(_kn5, car?.AcdData) {
                        ΘFrom = (float)From,
                        ΘTo = (float)To,
                        Iterations = Iterations,
                        SkyBrightnessLevel = (float)Brightness / 100f,
                        Gamma = (float)Gamma / 100f,
                        Ambient = (float)Ambient / 100f,
                        ShadowBias = (float)ShadowBias / 100f
                    }) {
                        renderer.CopyStateFrom(_renderer as ToolsKn5ObjectRenderer);
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, _textureName, progress, cancellation);
                    }
                });

                if (cancellation.IsCancellationRequested) return null;
            }

            return new Size(width, height);
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
            var resultSizeN = await CalculateAo(FlexibleParser.TryParseInt(o), filename, _car);
            if (!resultSizeN.HasValue) return;

            var resultSize = resultSizeN.Value;
            var uniquePostfix = GetShortChecksum(_kn5.OriginalFilename);
            var originalTexture = FilesStorage.Instance.GetTemporaryFilename(
                    $"{FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(_textureName))} Original ({uniquePostfix}).tmp");
            if (File.Exists(originalTexture)) {
                new ImageViewer(new object[] { filename, originalTexture }) {
                    Model = {
                        Saveable = true,
                        SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                        SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                        MaxImageWidth = resultSize.Width,
                        MaxImageHeight = resultSize.Height,
                    }
                }.ShowDialog();
                return;
            }

            byte[] data;
            if (_renderer != null && _kn5.TexturesData.TryGetValue(_textureName, out data)) {
                var image = CarTextureDialog.LoadImageUsingDirectX(_renderer, data);
                if (image != null) {
                    image.Image.SaveAsPng(originalTexture);
                    new ImageViewer(new object[] { filename, originalTexture }) {
                        Model = {
                            Saveable = true,
                            SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                            SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                            MaxImageWidth = resultSize.Width,
                            MaxImageHeight = resultSize.Height,
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

        public CarTextureDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5,
                [NotNull] string textureName, uint materialId) {
            DataContext = new ViewModel(renderer, car, activeSkin, kn5, textureName, materialId) { Close = () => Close() };
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        public class ViewModel : NotifyPropertyChanged {
            internal Action Close;

            [CanBeNull]
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;
            private readonly Kn5 _kn5;
            private readonly uint _materialId;

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

            public byte[] Data { get; set; }

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

            private Kn5Material _material;

            [CanBeNull]
            public Kn5Material Material {
                get { return _material; }
                set {
                    if (Equals(value, _material)) return;
                    _material = value;
                    OnPropertyChanged();
                }
            }

            private string _usedFor;

            public string UsedFor {
                get { return _usedFor; }
                set {
                    if (Equals(value, _usedFor)) return;
                    _usedFor = value;
                    OnPropertyChanged();
                }
            }

            public bool IsForkAvailable { get; }

            public bool IsChangeAvailable { get; }

            public ViewModel([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] Kn5 kn5, 
                    [NotNull] string textureName, uint materialId) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                _materialId = materialId;

                TextureName = textureName;
                Material = kn5.GetMaterial(materialId);

                var format = Regex.Match(textureName, @"(?<=\.)([a-zA-Z]{3,4})$").Value;
                TextureFormat = string.IsNullOrWhiteSpace(format) ? null : format.ToUpperInvariant();

                byte[] data;
                Data = kn5.TexturesData.TryGetValue(textureName, out data) ? data : null;

                BakedShadows = new BakedShadowsRendererViewModel(renderer, _kn5, TextureName, car);

                var usedFor = (from material in kn5.Materials.Values
                               let slots = (from slot in material.TextureMappings
                                            where string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)
                                            select slot.Name).ToList()
                               where slots.Count > 0
                               orderby material.Name
                               select $"{material.Name} ({slots.JoinToString(", ")})").ToList();
                IsForkAvailable = usedFor.Count > 1;
                IsChangeAvailable = kn5.TexturesData.Count > 1;
                UsedFor = usedFor.JoinToString(", ");
            }

            public async void OnLoaded() {
                Loading = true;

                var loaded = _renderer == null ? await LoadImageUsingMagickNet(Data) : LoadImageUsingDirectX(_renderer, Data);
                PreviewImage = loaded?.Image;
                TextureFormatDescription = loaded?.FormatDescription;
                TextureDimensions = PreviewImage == null ? null : $"{PreviewImage.PixelWidth}×{PreviewImage.PixelHeight}";
                BakedShadows.Size = PreviewImage == null ? (Size?)null : new Size(PreviewImage.PixelWidth, PreviewImage.PixelHeight);
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
                    Filter = string.Format("Textures (*.{0})|*.{0}", TextureFormat.ToLower()),
                    DefaultExt = TextureFormat.ToLower(),
                    FileName = TextureName
                };

                if (dialog.ShowDialog() != true) return;

                try {
                    using (WaitingDialog.Create("Saving…")) {
                        await FileUtils.WriteAllBytesAsync(dialog.FileName, Data);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.CustomShowroom_CannotExport, e);
                }
            }, () => Data != null));

            private async Task UpdateKn5() {
                await Task.Run(() => _kn5.Save(_kn5.OriginalFilename));

                var car = _activeSkin == null ? null : CarsManager.Instance.GetById(_activeSkin.CarId);
                if (car != null) {
                    (_renderer as ToolsKn5ObjectRenderer)?.MainSlot.SetCar(CarDescription.FromKn5(_kn5, car.Location, car.AcdData));
                }

                Close?.Invoke();
            }

            private AsyncCommand _replaceCommand;

            public AsyncCommand ReplaceCommand => _replaceCommand ?? (_replaceCommand = new AsyncCommand(async () => {
                var dialog = new OpenFileDialog {
                    InitialDirectory = _activeSkin?.Location ?? Path.GetDirectoryName(_kn5.OriginalFilename),
                    Filter = FileDialogFilters.TexturesFilter,
                    FileName = TextureName
                };

                if (dialog.ShowDialog() != true) return;
                
                try {
                    var info = new FileInfo(dialog.FileName);
                    if (!string.Equals(info.Extension, ".dds", StringComparison.OrdinalIgnoreCase)) {
                        if (ShowMessage("Texture is not in DDS format. Are you sure you want to use it?", "Wrong format", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                            return;
                        }
                    } else if (info.Length > 30e6) {
                        if (ShowMessage("Texture is way too big. Are you sure you want to use it?", "Way too big", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                            return;
                        }
                    }
                    
                    using (WaitingDialog.Create("Replacing…")) {
                        _kn5.TexturesData[TextureName] = await FileUtils.ReadAllBytesAsync(dialog.FileName);
                        await UpdateKn5();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t replace texture", e);
                }
            }));

            private AsyncCommand _renameCommand;

            public AsyncCommand RenameCommand => _renameCommand ?? (_renameCommand = new AsyncCommand(async () => {
                var newName = Prompt.Show("New texture name:", "Rename texture", TextureName, "?", required: true, maxLength: 120)?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t rename texture", "Name already taken.");
                    }

                    using (WaitingDialog.Create("Renaming…")) {
                        _kn5.TexturesData[newName] = _kn5.TexturesData[TextureName];
                        _kn5.Textures[newName] = _kn5.Textures[TextureName];
                        _kn5.Textures[newName].Name = newName;

                        _kn5.TexturesData.Remove(TextureName);
                        _kn5.Textures.Remove(TextureName);

                        foreach (var material in _kn5.Materials.Values) {
                            foreach (var mapping in material.TextureMappings) {
                                if (string.Equals(mapping.Texture, TextureName, StringComparison.OrdinalIgnoreCase)) {
                                    mapping.Texture = newName;
                                }
                            }
                        }
                        await UpdateKn5();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t rename texture", e);
                }
            }));

            private AsyncCommand _forkCommand;

            public AsyncCommand ForkCommand => _forkCommand ?? (_forkCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var newName = Prompt.Show("New texture name:", "Fork texture", TextureName, "?", required: true, maxLength: 120)?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try {
                    if (_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t fork texture", "Name already taken.");
                    }

                    using (WaitingDialog.Create("Forking…")) {
                        var usedElsewhere = _kn5.Materials.Values.ApartFrom(material).Any(m =>
                                m.TextureMappings.Any(slot => string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)));

                        _kn5.TexturesData[newName] = _kn5.TexturesData[TextureName];
                        _kn5.Textures[newName] = _kn5.Textures[TextureName].Clone();
                        _kn5.Textures[newName].Name = newName;

                        if (!usedElsewhere) {
                            _kn5.TexturesData.Remove(TextureName);
                            _kn5.Textures.Remove(TextureName);
                        }

                        foreach (var mapping in material.TextureMappings) {
                            if (string.Equals(mapping.Texture, TextureName, StringComparison.OrdinalIgnoreCase)) {
                                mapping.Texture = newName;
                            }
                        }
                        await UpdateKn5();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t fork texture", e);
                }
            }));

            private AsyncCommand _changeTextureCommand;

            public AsyncCommand ChangeTextureCommand => _changeTextureCommand ?? (_changeTextureCommand = new AsyncCommand(async () => {
                var material = Material;
                if (material == null) return;

                var newName = Prompt.Show("New texture name:", "Change texture", TextureName, "?", required: true, maxLength: 120,
                        suggestions: _kn5.Textures.Keys.OrderBy(x => x), suggestionsFixed: true)?.Trim();
                if (string.IsNullOrEmpty(newName) || newName == TextureName) return;

                try {
                    if (!_kn5.TexturesData.Keys.Contains(newName, StringComparer.OrdinalIgnoreCase)) {
                        throw new InformativeException("Can’t change texture", "Texture with that name not found.");
                    }

                    using (WaitingDialog.Create("Changing…")) {
                        var usedElsewhere = _kn5.Materials.Values.ApartFrom(material).Any(m =>
                                m.TextureMappings.Any(slot => string.Equals(slot.Texture, TextureName, StringComparison.OrdinalIgnoreCase)));

                        if (!usedElsewhere) {
                            _kn5.TexturesData.Remove(TextureName);
                            _kn5.Textures.Remove(TextureName);
                        }

                        foreach (var mapping in material.TextureMappings) {
                            if (string.Equals(mapping.Texture, TextureName, StringComparison.OrdinalIgnoreCase)) {
                                mapping.Texture = newName;
                            }
                        }

                        await UpdateKn5();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t change texture", e);
                }

            }));
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
