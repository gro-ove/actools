using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public static partial class PaintShop {
        public class ReferenceSolver {
            [CanBeNull]
            private string _jsonFilename;

            [CanBeNull]
            private ZipArchive _dataArchive;
            private bool _dataArchiveRead;

            [CanBeNull]
            public byte[] GetData([NotNull] string key) {
                if (_jsonFilename != null) {
                    var unpackedFilename = Path.Combine(_jsonFilename.ApartFromLast(@".json", StringComparison.OrdinalIgnoreCase), key);
                    if (File.Exists(unpackedFilename)) {
                        return File.ReadAllBytes(unpackedFilename);
                    }

                    if (!_dataArchiveRead) {
                        _dataArchiveRead = true;

                        var archive = _jsonFilename.ApartFromLast(@".json", StringComparison.OrdinalIgnoreCase) + @".zip";
                        if (File.Exists(archive)) {
                            try {
                                _dataArchive = ZipFile.OpenRead(archive);
                            } catch (Exception e) {
                                NonfatalError.Notify("Can’t read Paint Shop resouces", e);
                            }
                        }
                    }
                }

                return _dataArchive?.Entries.FirstOrDefault(x => string.Equals(x.FullName, key, StringComparison.OrdinalIgnoreCase))?
                                    .Open().ReadAsBytesAndDispose();
            }

            public IDisposable SetDataProvider(string jsonFilename) {
                var backup = new { _jsonFilename, _dataArchive, _dataArchiveRead };
                _jsonFilename = jsonFilename;
                _dataArchive = null;
                _dataArchiveRead = false;
                return new ActionAsDisposable(() => {
                    _dataArchive?.Dispose();
                    _jsonFilename = backup._jsonFilename;
                    _dataArchive = backup._dataArchive;
                    _dataArchiveRead = backup._dataArchiveRead;
                });
            }

            public IDisposable SetDataProvider(ZipArchive dataArchive) {
                var backup = new { _jsonFilename, _dataArchive, _dataArchiveRead };
                _jsonFilename = null;
                _dataArchive = dataArchive;
                _dataArchiveRead = true;
                return new ActionAsDisposable(() => {
                    _dataArchive?.Dispose();
                    _jsonFilename = backup._jsonFilename;
                    _dataArchive = backup._dataArchive;
                    _dataArchiveRead = backup._dataArchiveRead;
                });
            }

            [CanBeNull]
            private List<PaintableItem> _list;

            [CanBeNull]
            private PaintableItem GetSource(string key) {
                return _list?.FirstOrDefault(x => string.Equals(x.RefId, key, StringComparison.OrdinalIgnoreCase));
            }

            [NotNull]
            public ColorReference GetColorReference([NotNull] string ruleId, int colorIndex) {
                ColorReference result = null;
                var source = new Lazier<AspectsPaintableItem>(() => {
                    var sourceFound = GetSource(ruleId) as AspectsPaintableItem;
                    if (sourceFound != null) {
                        sourceFound.ColorChanged += (sender, args) => {
                            if (args.ColorIndex == null || args.ColorIndex == colorIndex) {
                                result?.RaiseUpdated();
                            }
                        };
                    }

                    return sourceFound;
                });

                result = new ColorReference(() => source.Value?.GetColor(colorIndex)?.ToColor(255));
                return result;
            }

            [CanBeNull]
            private List<TextureReference> _references;

            [NotNull]
            public TextureReference GetTextureReference([NotNull] string textureName) {
                if (_references == null) _references = new List<TextureReference>();

                var result = new TextureReference(textureName);
                _references.Add(result);
                return result;
            }

            public void SetRefList(List<PaintableItem> list) {
                if (_list != null) {
                    foreach (var item in _list.OfType<AspectsPaintableItem>()) {
                        item.TextureChanged -= OnTextureChanged;
                    }
                }

                _list = list;

                if (_list != null) {
                    foreach (var item in _list.OfType<AspectsPaintableItem>()) {
                        item.TextureChanged += OnTextureChanged;
                    }
                }
            }

            private void OnTextureChanged(object o, TextureChangedEventArgs e) {
                if (_references == null) return;

                for (var i = _references.Count - 1; i >= 0; i--) {
                    var reference = _references[i];
                    if (reference.TextureName == e.TextureName) {
                        reference.RaiseUpdated();
                    }
                }
            }
        }
    }
}