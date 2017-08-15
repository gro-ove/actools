using System;
using System.ComponentModel;
using System.IO;
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
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public class BakedShadowsRendererViewModel : INotifyPropertyChanged, IUserPresetable {
        public static readonly string PresetableKey = "Baked Shadows";
        public static readonly PresetsCategory PresetableKeyCategory = new PresetsCategory(PresetableKey);

        private class SaveableData {
            public double From, To = 60d, Brightness = 220d, Gamma = 60d, Ambient, ShadowBias;
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
        private double _from;

        public double From {
            get => _from;
            set {
                if (Equals(value, _from)) return;
                _from = value;
                OnPropertyChanged();
            }
        }

        private double _to = 60;

        public double To {
            get => _to;
            set {
                if (Equals(value, _to)) return;
                _to = value;
                OnPropertyChanged();
            }
        }

        private double _brightness = 220;

        public double Brightness {
            get => _brightness;
            set {
                if (Equals(value, _brightness)) return;
                _brightness = value;
                OnPropertyChanged();
            }
        }

        private double _gamma = 60;

        public double Gamma {
            get => _gamma;
            set {
                if (Equals(value, _gamma)) return;
                _gamma = value;
                OnPropertyChanged();
            }
        }

        private double _ambient;

        public double Ambient {
            get => _ambient;
            set {
                if (Equals(value, _ambient)) return;
                _ambient = value;
                OnPropertyChanged();
            }
        }

        private double _shadowBias;

        public double ShadowBias {
            get => _shadowBias;
            set {
                if (Equals(value, _shadowBias)) return;
                _shadowBias = value;
                OnPropertyChanged();
            }
        }

        private int _iterations = 5000;

        public int Iterations {
            get => _iterations;
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

                    var match = Regex.Match(result, @"^\s*(\d+)(?:\s+|\s*\D\s*)(\d+)\s*$");
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
                    width = height = size ?? 1024;
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
                var image = Kn5TextureDialog.LoadImageUsingDirectX(_renderer, data);
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
}