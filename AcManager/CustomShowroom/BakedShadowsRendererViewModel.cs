using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public class BakedShadowsRendererViewModel : NotifyPropertyChanged, IUserPresetable {
        public static readonly string PresetableKey = "Baked Shadows";
        public static readonly PresetsCategory PresetableKeyCategory = new PresetsCategory(PresetableKey);

        private class SaveableData {
            public double From, To = 60d, Brightness = 220d, Gamma = 60d, PixelDensity = 4d, Ambient, ShadowBias, ShadowBiasCullBack = 70d;
            public int Iterations = 5000, Padding = 4, ShadowMapSize = 2048;
            public bool UseFxaa = true;
        }

        [CanBeNull]
        private readonly BaseRenderer _renderer;

        private readonly Kn5 _kn5;
        private readonly ISaveHelper _saveable;

        [NotNull]
        public readonly string TextureName;

        [CanBeNull]
        public readonly string ObjectPath;

        private Size? _originSize;

        public Size? OriginSize {
            get => _originSize;
            set {
                if (value.Equals(_originSize)) return;
                _originSize = value;
                _size = value;
                OnPropertyChanged();
            }
        }

        private Size? _size;

        [CanBeNull]
        private readonly CarObject _car;

        public static BakedShadowsRendererViewModel ForTexture([CanBeNull] BaseRenderer renderer, [NotNull] Kn5 kn5, [NotNull] string textureName,
                [CanBeNull] CarObject car) {
            return new BakedShadowsRendererViewModel(renderer, kn5, textureName, null, car);
        }

        public static BakedShadowsRendererViewModel ForObject([CanBeNull] BaseRenderer renderer, [NotNull] Kn5 kn5, [NotNull] string objectPath,
                [CanBeNull] CarObject car) {
            return new BakedShadowsRendererViewModel(renderer, kn5, null, objectPath, car);
        }

        private BakedShadowsRendererViewModel([CanBeNull] BaseRenderer renderer, [NotNull] Kn5 kn5,
                [CanBeNull] string textureName, [CanBeNull] string objectPath, [CanBeNull] CarObject car) {
            _renderer = renderer;
            _kn5 = kn5;

            if (textureName == null) {
                if (objectPath == null) throw new ArgumentNullException(nameof(objectPath));

                var node = _kn5.GetNode(objectPath)
                        ?? throw new Exception($"Node “{objectPath}” not found");
                var material = _kn5.GetMaterial(node.MaterialId)
                        ?? throw new Exception($"Material for node “{objectPath}” not found");
                textureName = material.GetMappingByName("txDiffuse")?.Texture ?? material.TextureMappings.FirstOrDefault()?.Texture
                        ?? throw new Exception($"Texture for node “{objectPath}” not found");

                TextureName = textureName;
                ObjectPath = objectPath;
            }

            TextureName = textureName;

            _car = car;
            _saveable = new SaveHelper<SaveableData>("_carTextureDialog", () => new SaveableData {
                From = From,
                To = To,
                Brightness = Brightness,
                Gamma = Gamma,
                Ambient = Ambient,
                Iterations = Iterations,
                ShadowBias = ShadowBiasCullFront,
                ShadowBiasCullBack = ShadowBiasCullBack,
                PixelDensity = PixelDensity,
                Padding = Padding,
                ShadowMapSize = ShadowMapSize,
                UseFxaa = UseFxaa,
            }, o => {
                From = o.From;
                To = o.To;
                Brightness = o.Brightness;
                Gamma = o.Gamma;
                Ambient = o.Ambient;
                Iterations = o.Iterations;
                ShadowBiasCullFront = o.ShadowBias;
                ShadowBiasCullBack = o.ShadowBiasCullBack;
                PixelDensity = o.PixelDensity;
                Padding = o.Padding;
                ShadowMapSize = o.ShadowMapSize;
                UseFxaa = o.UseFxaa;
            });

            _saveable.Initialize();
        }

        #region Properties
        private double _from;

        public double From {
            get => _from;
            set => Apply(value, ref _from);
        }

        private double _to = 60;

        public double To {
            get => _to;
            set => Apply(value, ref _to);
        }

        private double _brightness = 220;

        public double Brightness {
            get => _brightness;
            set => Apply(value, ref _brightness);
        }

        private double _gamma = 60;

        public double Gamma {
            get => _gamma;
            set => Apply(value, ref _gamma);
        }

        private double _ambient;

        public double Ambient {
            get => _ambient;
            set => Apply(value, ref _ambient);
        }

        private double _shadowBiasCullFront;

        public double ShadowBiasCullFront {
            get => _shadowBiasCullFront;
            set => Apply(value, ref _shadowBiasCullFront);
        }

        private double _shadowBiasCullBack;

        public double ShadowBiasCullBack {
            get => _shadowBiasCullBack;
            set => Apply(value, ref _shadowBiasCullBack);
        }

        private int _iterations = 5000;

        public int Iterations {
            get => _iterations;
            set {
                value = value.Clamp(10, 1000000);
                if (Equals(value, _iterations)) return;
                _iterations = value;
                OnPropertyChanged();
            }
        }

        private double _pixelDensity = 4d;

        public double PixelDensity {
            get => _pixelDensity;
            set {
                value = value.Clamp(0.1, 16d);
                if (Equals(value, _pixelDensity)) return;
                _pixelDensity = value;
                OnPropertyChanged();
            }
        }

        private int _padding;

        public int Padding {
            get => _padding;
            set {
                value = value.Clamp(0, 1000);
                if (Equals(value, _padding)) return;
                _padding = value;
                OnPropertyChanged();
            }
        }

        private bool _useFxaa;

        public bool UseFxaa {
            get => _useFxaa;
            set => Apply(value, ref _useFxaa);
        }

        private int _shadowMapSize;

        public int ShadowMapSize {
            get => _shadowMapSize;
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShadowMapSizeSetting));
            }
        }

        public SettingEntry ShadowMapSizeSetting {
            get => DarkRendererSettings.ShadowResolutions.GetByIdOrDefault<SettingEntry, int?>(ShadowMapSize) ??
                    new SettingEntry(ShadowMapSize, $"{ShadowMapSize}×{ShadowMapSize}");
            set => ShadowMapSize = value.IntValue ?? 2048;
        }
        #endregion

        #region Generating
        private const string KeyDimensions = "__BakedShadowsRendererViewModel.Dimensions";

        public async Task<Size?> CalculateAo(int? size, string filename, [CanBeNull] CarObject car) {
            int width, height;
            switch (size) {
                case null:
                    var result = Prompt.Show(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                            ValuesStorage.Get(KeyDimensions, _size.HasValue ? $"{_size?.Width}x{_size?.Height}" : ""), @"2048x2048");
                    if (string.IsNullOrWhiteSpace(result)) return null;

                    ValuesStorage.Set(KeyDimensions, result);

                    var match = Regex.Match(result, @"^\s*(\d+)(?:\s+|\s*\D\s*)(\d+)\s*$");
                    if (match.Success) {
                        width = FlexibleParser.ParseInt(match.Groups[1].Value);
                        height = FlexibleParser.ParseInt(match.Groups[2].Value);
                    } else {
                        if (FlexibleParser.TryParseInt(result, out var value)) {
                            width = height = value;
                        } else {
                            NonfatalError.Notify(ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed,
                                    ControlsStrings.CustomShowroom_ViewMapping_ParsingFailed_Commentary);
                            return null;
                        }
                    }
                    break;

                case -1:
                    width = (int)(_size?.Width ?? 1024);
                    height = (int)(_size?.Height ?? 1024);
                    break;

                default:
                    width = height = size.Value;
                    break;
            }

            using (var waiting = new WaitingDialog(reportValue: "Rendering…") {
                Topmost = false
            }) {
                var cancellation = waiting.CancellationToken;
                var progress = (IProgress<double>)waiting;

                await Task.Run(() => {
                    using (var renderer = new BakedShadowsRenderer(_kn5, car?.AcdData) {
                        ΘFromDeg = (float)From,
                        ΘToDeg = (float)To,
                        Iterations = Iterations,
                        SkyBrightnessLevel = (float)Brightness / 100f,
                        Gamma = (float)Gamma / 100f,
                        Ambient = (float)Ambient / 100f,
                        ShadowBiasCullFront = (float)ShadowBiasCullFront / 100f,
                        ShadowBiasCullBack = (float)ShadowBiasCullBack / 100f,
                        UseFxaa = UseFxaa,
                        Padding = Padding,
                        MapSize = ShadowMapSize,
                        ResolutionMultiplier = Math.Sqrt(PixelDensity)
                    }) {
                        renderer.CopyStateFrom(_renderer as ToolsKn5ObjectRenderer);
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, TextureName, ObjectPath, progress, cancellation);
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
            try {
                var filename = FilesStorage.Instance.GetTemporaryFilename(
                        $"{FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(TextureName))} AO.png");
                var resultSizeN = await CalculateAo(FlexibleParser.TryParseInt(o), filename, _car);
                if (!resultSizeN.HasValue) return;

                var resultSize = resultSizeN.Value;
                var uniquePostfix = GetShortChecksum(_kn5.OriginalFilename);
                var originalTexture = FilesStorage.Instance.GetTemporaryFilename(
                        $"{FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(TextureName))} Original ({uniquePostfix}).tmp");
                if (File.Exists(originalTexture)) {
                    new ImageViewer(new[] { filename, originalTexture }, detailsCallback: DetailsCallback) {
                        MaxImageWidth = resultSize.Width,
                        MaxImageHeight = resultSize.Height,
                        Model = {
                            Saveable = true,
                            SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                            SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                            SaveDialogFilterPieces = {
                                DialogFilterPiece.DdsFiles,
                                DialogFilterPiece.JpegFiles,
                                DialogFilterPiece.PngFiles,
                            },
                            SaveCallback = SaveCallback,
                            CanBeSavedCallback = i => i == 0
                        },
                        ShowInTaskbar = true
                    }.ShowDialog();
                    return;
                }

                if (_renderer != null && _kn5.TexturesData.TryGetValue(TextureName, out var data)) {
                    var image = Kn5TextureDialog.LoadImageUsingDirectX(_renderer, data);
                    if (image != null) {
                        image.Image?.ToBytes(ImageFormat.Png);
                        new ImageViewer(new[] { filename, originalTexture }, detailsCallback: DetailsCallback) {
                            MaxImageWidth = resultSize.Width,
                            MaxImageHeight = resultSize.Height,
                            Model = {
                                Saveable = true,
                                SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                                SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                                SaveDialogFilterPieces = {
                                    DialogFilterPiece.DdsFiles,
                                    DialogFilterPiece.JpegFiles,
                                    DialogFilterPiece.PngFiles,
                                },
                                SaveCallback = SaveCallback,
                                CanBeSavedCallback = i => i == 0
                            },
                            ShowInTaskbar = true
                        }.ShowDialog();
                        return;
                    }
                }

                new ImageViewer(filename) {
                    Model = {
                        Saveable = true,
                        SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                        SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename),
                        SaveDialogFilterPieces = {
                            DialogFilterPiece.DdsFiles,
                            DialogFilterPiece.JpegFiles,
                            DialogFilterPiece.PngFiles,
                        },
                        SaveCallback = SaveCallback
                    },
                    ShowInTaskbar = true,
                    ImageMargin = new Thickness()
                }.ShowDialog();

                object DetailsCallback(int index) {
                    return index == 0 ? "Generated AO map" : "Original texture";
                }

                Task SaveCallback(int index, string destination) {
                    return Task.Run(() => {
                        var extension = Path.GetExtension(destination)?.ToLowerInvariant();
                        switch (extension) {
                            case ".dds":
                                DdsEncoder.SaveAsDds(destination, File.ReadAllBytes(filename), PreferredDdsFormat.LuminanceTransparency, null);
                                break;
                            case ".jpg":
                            case ".jpeg":
                                ImageUtils.Convert(filename, destination);
                                break;
                            default:
                                File.Copy(filename, destination, true);
                                break;
                        }
                    });
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t create AO map", e);
            }
        }));
        #endregion

        #region Presetable
        public bool CanBeSaved => true;
        public PresetsCategory PresetableCategory => PresetableKeyCategory;
        string IUserPresetable.PresetableKey => PresetableKey;

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }
        #endregion

        [NotifyPropertyChangedInvocator]
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            base.OnPropertyChanged(propertyName);
            if (_saveable.SaveLater()) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}