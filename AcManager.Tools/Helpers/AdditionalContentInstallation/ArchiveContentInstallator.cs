using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;
using SevenZip;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class SevenZipExtension {
        public static bool HasAnyEncryptedFiles(this SevenZipExtractor extractor) {
            return extractor.ArchiveFileData.Any(x => x.Encrypted);
        }

        public static void ExtractFile(this SevenZipExtractor extractor, ArchiveFileInfo fileInfo, Stream stream) {
            extractor.ExtractFile(fileInfo.FileName, stream);
        }

        public static byte[] ExtractFile(this SevenZipExtractor extractor, ArchiveFileInfo fileInfo) {
            using (var stream = new MemoryStream()) {
                extractor.ExtractFile(fileInfo.FileName, stream);
                return stream.ToArray();
            }
        }
    }

    public class ArchiveContentInstallator : IAdditionalContentInstallator {
        public string Filename { get; private set; }

        public bool IsPasswordRequired { get; private set; }

        public bool IsPasswordCorrect {
            get { return !IsPasswordRequired || _extractor != null; }
        }

        public string PasswordValue {
            get { return _passwordValue; }
            set {
                if (Equals(value, _passwordValue)) return;
                _passwordValue = value;

                if (_extractor != null) {
                    _extractor.Dispose();
                    _extractor = null;
                }

                _extractor = CreateExtractor();
            }
        }

        private SevenZipExtractor _extractor;

        internal ArchiveContentInstallator(string filename) {
            Filename = filename;
            SafelyCreateExtractor();
        }

        private SevenZipExtractor CreateExtractor() {
            try {
                return PasswordValue == null
                           ? new SevenZipExtractor(Filename)
                           : new SevenZipExtractor(Filename, PasswordValue);
            } catch (SevenZipArchiveException e) {
                if (e.Message.Contains("Is it encrypted and a wrong password was provided?")) {
                    throw new PasswordException(PasswordValue == null
                                                    ? "Password is required"
                                                    : "Password is invalid");
                }

                throw;
            }
        }

        private bool HasAnyEncryptedFiles() {
            try {
                return _extractor.HasAnyEncryptedFiles();
            } catch (SevenZipArchiveException e) {
                if (e.Message.Contains("Is it encrypted and a wrong password was provided?")) {
                    throw new PasswordException(PasswordValue == null
                                                    ? "Password is required"
                                                    : "Password is invalid");
                }

                throw;
            }
        }

        private void SafelyCreateExtractor() {
            try {
                _extractor = CreateExtractor();
                IsPasswordRequired = HasAnyEncryptedFiles();
            } catch (PasswordException) {
                IsPasswordRequired = true;
            }

            if (IsPasswordRequired && _extractor != null) {
                _extractor.Dispose();
                _extractor = null;
            }
        }

        private List<AdditionalContentEntry> _entries;
        private string _passwordValue;

        private IEnumerable<AdditionalContentEntry> GetEntries() {
            using (var extractor = CreateExtractor()) {
                var fileData = extractor.ArchiveFileData.ToIReadOnlyListIfItsNot();
                foreach (var fileInfo in fileData) {
                    var filename = (Path.GetFileName(fileInfo.FileName) ?? "").ToLower();

                    AdditionalContentType? type;

                    switch (filename) {
                        case "ui_car.json":
                            type = AdditionalContentType.Car;
                            break;

                        case "ui_skin.json":
                            type = AdditionalContentType.CarSkin;
                            break;

                        case "ui_track.json":
                            type = AdditionalContentType.Track;
                            break;

                        case "ui_showroom.json":
                            type = AdditionalContentType.Showroom;
                            break;

                        default:
                            continue;
                    }

                    var entryDirectory = Path.GetDirectoryName(Path.GetDirectoryName(fileInfo.FileName));
                    var id =
                        (Path.GetFileName(entryDirectory) ?? Path.GetFileNameWithoutExtension(Filename) ?? "").ToLower();

                    JObject jObject;

                    try {
                        var jsonBytes = extractor.ExtractFile(fileInfo);
                        jObject = JsonExtension.Parse(jsonBytes.GetString());
                    } catch (ExtractionFailedException) {
                        throw;
                    } catch (Exception) {
                        jObject = null;
                    }

                    if (id.Length == 0) {
                        // TODO
                        continue;
                    }

                    if (jObject == null) {
                        // TODO
                        continue;
                    }

                    yield return new AdditionalContentEntry {
                        Id = id,
                        Type = type.Value,
                        Name = jObject.GetStringValueOnly("name") ?? id,
                        Version = jObject.GetStringValueOnly("name") ?? id,
                        Path = entryDirectory
                    };
                }
            }
        }

        public void InstallEntryTo(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory) {
            throw new NotImplementedException();
        }

        public IReadOnlyList<AdditionalContentEntry> Entries {
            get { return _entries ?? (_entries = GetEntries().ToList()); }
        }

        public void Dispose() {
            if (_extractor != null) {
                _extractor.Dispose();
                _extractor = null;
            }
        }
    }
}
