using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SevenZip;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class SevenZipExtension {
        public static bool HasAnyEncryptedFiles(this SevenZipExtractor extractor) {
            return extractor.ArchiveFileData.Any(x => x.Encrypted);
        }

        public static string GetFilename(this ArchiveFileInfo fileInfo) {
            return fileInfo.FileName.Replace('/', '\\');
        }

        public static string GetName(this ArchiveFileInfo fileInfo) {
            return Path.GetFileName(fileInfo.FileName ?? "");
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
        public string Filename { get; }

        public bool IsPasswordRequired { get; private set; }

        public bool IsPasswordCorrect => !IsPasswordRequired || _extractor != null;

        public string PasswordValue {
            get { return _passwordValue; }
            set {
                if (Equals(value, _passwordValue)) return;
                _passwordValue = value;

                DisposeHelper.Dispose(ref _extractor);
                _extractor = CreateExtractor();
            }
        }

        [CanBeNull]
        private SevenZipExtractor _extractor;

        internal ArchiveContentInstallator(string filename) {
            Filename = filename;
            SafelyCreateExtractor();
        }

        private SevenZipExtractor CreateExtractor() {
            try {
                return PasswordValue == null ? new SevenZipExtractor(Filename)
                        : new SevenZipExtractor(Filename, PasswordValue);
            } catch (SevenZipArchiveException e) {
                if (e.Message.Contains("Is it encrypted and a wrong password was provided?")) {
                    throw new PasswordException(PasswordValue == null ? "Password is required" :
                            "Password is invalid");
                }

                throw;
            }
        }

        private bool HasAnyEncryptedFiles() {
            try {
                return _extractor?.HasAnyEncryptedFiles() == true;
            } catch (SevenZipArchiveException e) {
                if (e.Message.Contains("Is it encrypted and a wrong password was provided?")) {
                    throw new PasswordException(PasswordValue == null ? "Password is required" :
                            "Password is invalid");
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

            if (IsPasswordRequired) {
                DisposeHelper.Dispose(ref _extractor);
            }
        }

        private List<AdditionalContentEntry> _entries;
        private string _passwordValue;

        private IEnumerable<AdditionalContentEntry> GetEntries() {
            var found = new List<string>();
            using (var extractor = CreateExtractor()) {
                foreach (var fileInfo in extractor.ArchiveFileData.Where(x => !x.IsDirectory)) {
                    var filename = fileInfo.GetName().ToLower();

                    AdditionalContentType type;
                    string entryDirectory;

                    switch (filename) {
                        case "ui_car.json": {
                                type = AdditionalContentType.Car;
                                var directory = Path.GetDirectoryName(fileInfo.GetFilename());
                                if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                                entryDirectory = Path.GetDirectoryName(directory);
                                break;
                            }

                        case "//ui_skin.json": {
                                // TODO (disabled atm)
                                // TODO: detect by preview.jpg & livery.png
                                type = AdditionalContentType.CarSkin;
                                entryDirectory = Path.GetDirectoryName(fileInfo.GetFilename());
                                break;
                            }

                        case "ui_track.json": {
                                type = AdditionalContentType.Track;
                                var directory = Path.GetDirectoryName(fileInfo.GetFilename());
                                if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) {
                                    directory = Path.GetDirectoryName(directory);
                                }
                                if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                                entryDirectory = Path.GetDirectoryName(directory);
                                break;
                            }

                        case "ui_showroom.json": {
                                type = AdditionalContentType.Showroom;
                                var directory = Path.GetDirectoryName(fileInfo.GetFilename());
                                if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                                entryDirectory = Path.GetDirectoryName(directory);
                                break;
                            }

                        default:
                            continue;
                    }

                    if (entryDirectory == null) {
                        Logging.Warning("Entry directory is null: " + fileInfo.GetFilename());
                        continue;
                    }

                    if (found.Contains(entryDirectory)) continue;
                    found.Add(entryDirectory);

                    var id = entryDirectory != string.Empty ? Path.GetFileName(entryDirectory) : Path.GetFileNameWithoutExtension(Filename);
                    if (string.IsNullOrEmpty(id)) {
                        Logging.Warning("ID is empty: " + fileInfo.GetFilename());
                        continue;
                    }

                    JObject jObject;
                    try {
                        var jsonBytes = extractor.ExtractFile(fileInfo);
                        jObject = JsonExtension.Parse(jsonBytes.GetString());
                    } catch (ExtractionFailedException) {
                        throw;
                    } catch (Exception e) {
                        Logging.Warning("Can't read as a JSON (" + fileInfo.GetFilename() + "): " + e);
                        continue;
                    }

                    yield return new AdditionalContentEntry(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
                            jObject.GetStringValueOnly("version"));
                }
            }
        }

        void IAdditionalContentInstallator.InstallEntryTo(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory) {
            using (var extractor = CreateExtractor()) {
                foreach (var fileInfo in extractor.ArchiveFileData.Where(x => !x.IsDirectory)) {
                    var filename = fileInfo.GetFilename();
                    if (entry.Path != string.Empty && !FileUtils.IsAffected(entry.Path, filename)) continue;

                    var subFilename = filename.SubstringExt(entry.Path.Length);
                    if (subFilename.StartsWith("\\")) {
                        subFilename = subFilename.Substring(1);
                    }

                    if (filter == null || filter(subFilename)) {
                        var path = Path.Combine(targetDirectory, subFilename);
                        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? targetDirectory);
                        File.WriteAllBytes(path, extractor.ExtractFile(fileInfo));
                    }
                }
            }
        }

        IReadOnlyList<AdditionalContentEntry> IAdditionalContentInstallator.Entries => _entries ?? (_entries = GetEntries().ToList());

        void IDisposable.Dispose() {
            DisposeHelper.Dispose(ref _extractor);
        }
    }
}
