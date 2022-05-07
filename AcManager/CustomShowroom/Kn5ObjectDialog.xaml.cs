using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public partial class Kn5ObjectDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public Kn5ObjectDialog([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] IKn5 kn5,
                [NotNull] IKn5RenderableObject renderableObject) {
            DataContext = new ViewModel(renderer, car, activeSkin, kn5, renderableObject) { Close = () => Close() };
            InitializeComponent();

            Buttons = new[] { CloseButton };
        }

        public class ViewModel : NotifyPropertyChanged {
            internal Action Close;

            [CanBeNull]
            private readonly BaseRenderer _renderer;

            [CanBeNull]
            private readonly CarSkinObject _activeSkin;

            private readonly IKn5 _kn5;

            [NotNull]
            private readonly IKn5RenderableObject _renderableObject;

            public BakedShadowsRendererViewModel BakedShadows { get; }

            [NotNull]
            public string ObjectName { get; }

            [NotNull]
            public string BaseObjectPath { get; }

            [NotNull]
            public string ParentObjectPath { get; }

            public int VerticesCount { get; }
            public int TrianglesCount { get; }
            public string Flags { get; }

            [CanBeNull]
            public Kn5Material Material { get; }

            private string _textureDimensions;

            public string TextureDimensions {
                get => _textureDimensions;
                set => Apply(value, ref _textureDimensions);
            }

            public ViewModel([CanBeNull] BaseRenderer renderer, [CanBeNull] CarObject car, [CanBeNull] CarSkinObject activeSkin, [NotNull] IKn5 kn5,
                    [NotNull] IKn5RenderableObject renderableObject) {
                _renderer = renderer;
                _activeSkin = activeSkin;
                _kn5 = kn5;
                _renderableObject = renderableObject;

                var flagsList = new[] {
                    renderableObject.OriginalNode.IsTransparent ? "Transparent" : null,
                    // renderableObject.OriginalNode.IsRenderable ? "Renderable" : null,
                    renderableObject.OriginalNode.IsVisible ? "Visible" : null,
                    renderableObject.OriginalNode.Layer != 0 ? $"Layer #{renderableObject.OriginalNode.Layer}" : null,
                }.NonNull().ToList();
                var flags = flagsList.Count == 0 ? "None" : flagsList.JoinToString(", ");
                Flags = flags.Length > 0 ? char.ToUpper(flags.FirstOrDefault()) + flags.Substring(1) : "";

                ObjectName = renderableObject.OriginalNode.Name;
                VerticesCount = renderableObject.OriginalNode.Vertices.Length;
                TrianglesCount = renderableObject.TrianglesCount;
                Material = kn5.GetMaterial(renderableObject.OriginalNode.MaterialId);

                BaseObjectPath = kn5.GetObjectPath(renderableObject.OriginalNode) ?? throw new Exception("Can’t determine object path");
                ParentObjectPath = kn5.GetParentPath(renderableObject.OriginalNode) ?? throw new Exception("Can’t determine parent path");
                BakedShadows = BakedShadowsRendererViewModel.ForObject(renderer, _kn5, BaseObjectPath, car);
            }

            public async void OnLoaded() {
                _kn5.TexturesData.TryGetValue(BakedShadows.TextureName, out var data);
                var loaded = _renderer == null
                        ? await Kn5TextureDialog.LoadImageUsingMagickNetAsync(data)
                        : await Task.Run(() => Kn5TextureDialog.LoadImageUsingDirectX(_renderer, data));
                TextureDimensions = loaded?.Image == null ? null : $"{loaded.Image.PixelWidth}×{loaded.Image.PixelHeight}";
                BakedShadows.OriginSize = loaded?.Image == null ? (Size?)null : new Size(loaded.Image.PixelWidth, loaded.Image.PixelHeight);
            }

            private const string KeyDimensions = "__CarTextureDialog.Dimensions";

            private AsyncCommand<string> _uvCommand;

            public AsyncCommand<string> UvCommand => _uvCommand ?? (_uvCommand = new AsyncCommand<string>(async o => {
                var size = FlexibleParser.TryParseInt(o);
                var filename = FilesStorage.Instance.GetTemporaryFilename(
                        FileUtils.EnsureFileNameIsValid(Path.GetFileNameWithoutExtension(ObjectName), true) + " UV.png");

                int width, height;
                switch (size) {
                    case null:
                        var result = await Prompt.ShowAsync(ControlsStrings.CustomShowroom_ViewMapping_Prompt, ControlsStrings.CustomShowroom_ViewMapping,
                                ValuesStorage.Get(KeyDimensions, BakedShadows.OriginSize != null ?
                                        $"{BakedShadows.OriginSize?.Width}x{BakedShadows.OriginSize?.Height}" : ""),
                                @"2048x2048");
                        if (string.IsNullOrWhiteSpace(result)) return;

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
                                return;
                            }
                        }
                        break;

                    case -1:
                        width = (int)(BakedShadows.OriginSize?.Width ?? 1024);
                        height = (int)(BakedShadows.OriginSize?.Height ?? 1024);
                        break;

                    default:
                        width = height = size ?? 1024;
                        break;
                }

                await Task.Run(() => {
                    using (var renderer = new UvRenderer(_kn5)) {
                        renderer.Width = width;
                        renderer.Height = height;
                        renderer.Shot(filename, null, BaseObjectPath);
                    }
                });

                new ImageViewer(filename) {
                    Model = {
                        Saveable = true,
                        SaveableTitle = ControlsStrings.CustomShowroom_ViewMapping_Export,
                        SaveDirectory = Path.GetDirectoryName(_kn5.OriginalFilename)
                    },
                    ImageMargin = new Thickness()
                }.ShowDialog();
            }));
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
    }
}