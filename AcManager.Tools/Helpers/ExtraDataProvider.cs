using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace AcManager.Tools.Helpers {
    public class ExtraDataProvider : IDisposable {
        public ExtraDataProvider(string originFilename) {
            _originFilename = Regex.Replace(originFilename, @"\.\w{2,5}$", "");
            _dataArchive = null;
            _dataArchiveRead = false;
        }

        public ExtraDataProvider(ZipArchive dataArchive) {
            _originFilename = null;
            _dataArchive = dataArchive;
            _dataArchiveRead = true;
        }

        public void Dispose() {
            _dataArchive?.Dispose();
        }

        [CanBeNull]
        private string _originFilename;

        [CanBeNull]
        private ZipArchive _dataArchive;

        private bool _dataArchiveRead;

        [CanBeNull]
        private ZipArchive GetDataArchive() {
            if (_originFilename != null && !_dataArchiveRead) {
                _dataArchiveRead = true;

                var archive = _originFilename + @".zip";
                if (File.Exists(archive)) {
                    try {
                        _dataArchive = ZipFile.OpenRead(archive);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read Paint Shop resouces", e);
                    }
                }
            }

            return _dataArchive;
        }

        [CanBeNull]
        public byte[] Get([Localizable(false), NotNull] string key) {
            if (_originFilename != null) {
                var unpackedFilename = Path.Combine(_originFilename, key);
                if (File.Exists(unpackedFilename)) {
                    return File.ReadAllBytes(unpackedFilename);
                }
            }

            return GetDataArchive()?.Entries.FirstOrDefault(x => string.Equals(x.FullName, key, StringComparison.OrdinalIgnoreCase))?
                                    .Open().ReadAsBytesAndDispose();
        }

        [CanBeNull]
        private string[] _dataList;

        [NotNull]
        public IEnumerable<string> GetKeys(string glob) {
            if (_originFilename == null) return new string[0];

            if (_dataList == null) {
                var directory = _originFilename;
                var dataList = new List<string>();
                if (Directory.Exists(directory)) {
                    dataList.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
                    for (var i = 0; i < dataList.Count; i++) {
                        dataList[i] = FileUtils.GetRelativePath(dataList[i], directory).Replace('\\', '/');
                    }

                    var fromArchive = GetDataArchive()?.Entries.Select(x => x.FullName);
                    if (fromArchive != null) {
                        dataList.AddRange(fromArchive);
                    }
                }

                _dataList = dataList.ToArray();
            }

            var regex = RegexFromQuery.Create(glob, StringMatchMode.CompleteMatch);
            return _dataList.Where(x => regex.IsMatch(x));
        }
    }
}