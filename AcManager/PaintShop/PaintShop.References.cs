using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public static partial class PaintShop {
        private class ReferenceSolver {
            private ExtraDataProvider _extraDataProvider;

            [CanBeNull]
            public byte[] GetData([NotNull] string key) {
                return _extraDataProvider?.Get(key);
            }

            [NotNull]
            public IEnumerable<string> GetKeys(string glob) {
                return _extraDataProvider?.GetKeys(glob) ?? new string[0];
            }

            public IDisposable SetDataProvider(string jsonFilename) {
                var backup = _extraDataProvider;
                var created = new ExtraDataProvider(jsonFilename);
                _extraDataProvider = created;
                return new ActionAsDisposable(() => {
                    created.Dispose();
                    _extraDataProvider = backup;
                });
            }

            public IDisposable SetDataProvider(ZipArchive dataArchive) {
                var backup = _extraDataProvider;
                var created = new ExtraDataProvider(dataArchive);
                _extraDataProvider = created;
                return new ActionAsDisposable(() => {
                    created.Dispose();
                    _extraDataProvider = backup;
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