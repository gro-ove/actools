using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
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

            [NotNull]
            public Func<Color?> GetColorReference([NotNull] string key) {
                return () => Color.DarkOrange;
            }

            public void SetRefList(List<PaintableItem> list) {

            }
        }
    }
}