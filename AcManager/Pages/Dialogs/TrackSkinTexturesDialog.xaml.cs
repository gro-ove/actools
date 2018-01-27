using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Kn5File;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificSpecial;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using SlimDX.DXGI;
using Application = System.Windows.Application;

namespace AcManager.Pages.Dialogs {
    public partial class TrackSkinTexturesDialog {
        private TrackSkinTexturesDialog(TrackObject track, TrackSkinObject trackSkin, IEnumerable<TextureEntry> images) {
            DataContext = new ViewModel(track, trackSkin, images);
            InitializeComponent();

            Buttons = new[] {
                CreateCloseDialogButton("Override", true, false, MessageBoxResult.OK, Model.SaveCommand),
                CancelButton
            };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public sealed class TextureEntry : Displayable {
            public string Key { get; }
            public BetterImage.BitmapEntry Image { get; }
            public string SourceKn5 { get; }
            public long Size { get; }
            public bool IsOverwritten { get; }

            public TextureEntry(string name, BetterImage.BitmapEntry image, string sourceKn5, long size, bool isOverwritten) {
                Key = name;
                Image = image;
                SourceKn5 = sourceKn5;
                Size = size;
                IsOverwritten = isOverwritten;
                DisplayName = name;
            }

            private bool _isSelected;

            public bool IsSelected {
                get => _isSelected;
                set {
                    if (Equals(value, _isSelected)) return;
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            private class SingleTextureLoader : IKn5TextureLoader {
                private readonly string _textureName;

                public SingleTextureLoader(string textureName) {
                    _textureName = textureName;
                }

                public void OnNewKn5(string kn5Filename) { }

                public byte[] Result { get; set; }

                public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                    if (textureName == _textureName) {
                        Result = reader.ReadBytes(textureSize);
                    } else {
                        reader.Skip(textureSize);
                    }
                    return null;
                }
            }

            private AsyncCommand _zoomCommand;

            public AsyncCommand ZoomCommand => _zoomCommand ?? (_zoomCommand = new AsyncCommand(async () => {
                try {
                    Tuple<BetterImage.BitmapEntry, Format> data;
                    using (WaitingDialog.Create("Loading texture…")) {
                        data = await Task.Run(() => {
                            var loader = new SingleTextureLoader(Key);
                            Kn5.FromFile(SourceKn5, loader, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance);
                            if (loader.Result == null) throw new Exception("Texture not found");

                            using (var reader = new TextureReader()) {
                                var image = BetterImage.LoadBitmapSourceFromBytes(reader.ToPng(loader.Result, true, out var format));
                                return Tuple.Create(image, format);
                            }
                        });
                    }

                    new ImageViewer(new object[] { data.Item1 }, details: GetDetails).ShowDialog();
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load texture", e);
                }

                object GetDetails(object o) => $"File name: {BbCodeBlock.Encode(Key)}\n"
                        + $"Size: {BbCodeBlock.Encode(Size.ToReadableSize())}\n"
                        + $"Source: {BbCodeBlock.Encode(SourceKn5)}";
            }));
        }

        public class ViewModel : NotifyPropertyChanged {
            public TrackObject Track { get; }
            public TrackSkinObject TrackSkin { get; }
            public ChangeableObservableCollection<TextureEntry> Images { get; }

            public ViewModel(TrackObject track, TrackSkinObject trackSkin, IEnumerable<TextureEntry> images) {
                Track = track;
                TrackSkin = trackSkin;
                Images = new ChangeableObservableCollection<TextureEntry>(images);
                Images.ItemPropertyChanged += OnItemPropertyChanged;
                UpdateSummary();
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(TextureEntry.IsSelected)) {
                    UpdateSummary();
                }
            }

            private void UpdateSummary() {
                var total = 0;
                var totalSize = 0L;
                foreach (var entry in Images.Where(x => x.IsSelected)) {
                    total++;
                    totalSize += entry.Size;
                }
                DisplaySummary = total == 0 ? null
                        : $"{PluralizingConverter.PluralizeExt(total, "{0} texture")} selected ({totalSize.ToReadableSize()})";
            }

            private string _displaySummary;

            public string DisplaySummary {
                get => _displaySummary;
                set {
                    if (Equals(value, _displaySummary)) return;
                    _displaySummary = value;
                    OnPropertyChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            private class SavingTextureLoader : IKn5TextureLoader {
                private readonly List<string> _toSave;
                private readonly string _destination;
                private readonly IProgress<string> _progress;

                public void OnNewKn5(string kn5Filename) { }

                public SavingTextureLoader(IEnumerable<string> toSave, string destination, IProgress<string> progress) {
                    _destination = destination;
                    _progress = progress;
                    _toSave = toSave.ToList();
                }

                public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                    if (_toSave.Contains(textureName)) {
                        _progress.Report(textureName);
                        var destination = Path.Combine(_destination, textureName);
                        if (!File.Exists(destination)) {
                            using (var file = File.OpenWrite(destination)) {
                                reader.CopyTo(file, textureSize);
                                return null;
                            }
                        }
                    }

                    reader.Skip(textureSize);
                    return null;
                }
            }

            private AsyncCommand _saveCommand;

            public AsyncCommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(async () => {
                try {
                    using (var waiting = WaitingDialog.Create("Saving textures…")) {
                        foreach (var kn5Group in Images.Where(x => x.IsSelected).GroupBy(x => x.SourceKn5)) {
                            await Task.Run(() => Kn5.FromFile(kn5Group.Key,
                                    new SavingTextureLoader(kn5Group.Select(x => x.Key), TrackSkin.Location,
                                            new Progress<string>(s => waiting.Report(Path.GetFileName(kn5Group.Key) + "/" + s))),
                                    SkippingMaterialLoader.Instance,
                                    SkippingNodeLoader.Instance));
                        }
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save textures", e);
                }
            }, () => DisplaySummary != null));
        }

        private class AsImagesLoader : IKn5TextureLoader {
            private string _kn5Filename;
            private readonly string _trackSkinLocation;
            private readonly TextureReader _textureReader;
            private readonly IProgress<string> _progress;
            private readonly Dictionary<string, TextureEntry> _result;

            public void OnNewKn5(string kn5Filename) {
                _kn5Filename = kn5Filename;
            }

            public AsImagesLoader(string trackSkinLocation, TextureReader textureReader, IProgress<string> progress, Dictionary<string, TextureEntry> result) {
                _trackSkinLocation = trackSkinLocation;
                _textureReader = textureReader;
                _progress = progress;
                _result = result;
            }

            public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                _progress?.Report($@"{Path.GetFileName(_kn5Filename)}/{textureName}");
                _result[textureName] = new TextureEntry(textureName,
                        BetterImage.LoadBitmapSourceFromBytes(_textureReader.ToPng(reader.ReadBytes(textureSize), true, new System.Drawing.Size(128, 128))),
                        _kn5Filename,
                        textureSize,
                        File.Exists(Path.Combine(_trackSkinLocation, textureName)));
                return null;
            }
        }

        public static async Task Run(TrackSkinObject trackSkin) {
            try {
                var track = TracksManager.Instance.GetById(trackSkin.TrackId);
                if (track == null) throw new Exception($"Track {trackSkin.TrackId} not found");

                var images = new Dictionary<string, TextureEntry>();
                using (var waiting = WaitingDialog.Create("Loading textures…"))
                using (var reader = new TextureReader()) {
                    var loader = new AsImagesLoader(trackSkin.Location, reader, waiting, images);
                    await Task.Run(() => {
                        var modelsFilename = track.ModelsFilename;
                        if (!File.Exists(modelsFilename)) {
                            var kn5Filename = Path.Combine(track.Location, track.Id + ".kn5");
                            if (!File.Exists(kn5Filename)) {
                                throw new Exception("Model not found");
                            }

                            Kn5.FromFile(kn5Filename, loader, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance);
                        } else {
                            new TrackComplexModelDescription(modelsFilename) {
                                TextureLoader = loader,
                                MaterialLoader = SkippingMaterialLoader.Instance,
                                NodeLoader = SkippingNodeLoader.Instance
                            }.GetEntries();
                        }
                    });
                }

                new TrackSkinTexturesDialog(track, trackSkin, images.Values.OrderBy(x => x.DisplayName)) {
                    Owner = Application.Current?.MainWindow
                }.ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load textures", e);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var item in e.AddedItems.OfType<TextureEntry>()) {
                item.IsSelected = true;
            }
            foreach (var item in e.RemovedItems.OfType<TextureEntry>()) {
                item.IsSelected = false;
            }
        }
    }
}