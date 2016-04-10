using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SharpCompress.Archive;
using SharpCompress.Common;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public class ArchiveContentInstallator : IAdditionalContentInstallator {
        public static async Task<IAdditionalContentInstallator> Create(string filename) {
            var result = new ArchiveContentInstallator(filename);
            await result.CreateExtractorAsync();
            return result;
        }

        [CanBeNull]
        private IArchive _extractor;

        public string Filename { get; }

        private ArchiveContentInstallator(string filename) {
            Filename = filename;
        }

        public bool IsPasswordRequired { get; private set; }

        public string Password { get; private set; }

        public bool IsPasswordCorrect => !IsPasswordRequired || _extractor != null;

        private async Task CreateExtractorAsync() {
            try {
                _extractor = await Task.Run(() => CreateExtractor(Filename, Password));
            } catch (PasswordException) {
                IsPasswordRequired = true;
                DisposeHelper.Dispose(ref _extractor);
            }
        }

        public Task TrySetPasswordAsync(string password) {
            Password = password;
            return CreateExtractorAsync();
        }

        private static IArchive CreateExtractor(string filename, string password) {
            try {
                var extractor = SharpCompressExtension.Open(filename, password);
                if (extractor.HasAnyEncryptedFiles()) {
                    throw new PasswordException(password == null ? "Password is required" :
                            "Password is invalid");
                }
                return extractor;
            } catch (CryptographicException) {
                throw new PasswordException(password == null ? "Password is required" :
                        "Password is invalid");
            }
        }

        public async Task<IReadOnlyList<AdditionalContentEntry>> GetEntriesAsync(IProgress<string> progress, CancellationToken cancellation) {
            if (_extractor == null) {
                throw new Exception("Extractor wasn't initialized");
            }

            var result = new List<AdditionalContentEntry>();
            var found = new List<string>();

            foreach (var fileInfo in _extractor.Entries.Where(x => !x.IsDirectory)) {
                var filename = fileInfo.GetName().ToLower();

                progress?.Report(filename);
                if (cancellation.IsCancellationRequested) break;

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
                    var jsonBytes = await fileInfo.ExtractFileAsync();
                    jObject = JsonExtension.Parse(jsonBytes.GetString());
                } catch (Exception e) {
                    Logging.Warning("Can't read as a JSON (" + fileInfo.GetFilename() + "): " + e);
                    continue;
                }

                result.Add(new AdditionalContentEntry(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
                        jObject.GetStringValueOnly("version")));
            }

            return result;
        }

        public async Task InstallEntryToAsync(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory, IProgress<string> progress, CancellationToken cancellation) {
            if (_extractor == null) {
                throw new Exception("Extractor wasn't initialized");
            }

            foreach (var fileInfo in _extractor.Entries.Where(x => !x.IsDirectory)) {
                var filename = fileInfo.GetFilename();
                if (entry.Path != string.Empty && !FileUtils.IsAffected(entry.Path, filename)) continue;

                var subFilename = filename.SubstringExt(entry.Path.Length);
                if (subFilename.StartsWith("\\")) {
                    subFilename = subFilename.Substring(1);
                }

                if (filter == null || filter(subFilename)) {
                    progress?.Report(subFilename);
                    if (cancellation.IsCancellationRequested) return;

                    var path = Path.Combine(targetDirectory, subFilename);
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? targetDirectory);

                    using (var fileStream = new FileStream(path, FileMode.Create))
                    using (var stream = fileInfo.OpenEntryStream()) {
                        await stream.CopyToAsync(fileStream);
                    }
                }
            }
        }

        void IDisposable.Dispose() {
            DisposeHelper.Dispose(ref _extractor);
            GC.Collect();
        }
    }
}
