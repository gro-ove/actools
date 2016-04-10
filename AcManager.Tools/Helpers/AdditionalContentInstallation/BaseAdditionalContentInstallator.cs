using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    internal abstract class BaseAdditionalContentInstallator : IAdditionalContentInstallator {
        public virtual Task TrySetPasswordAsync(string password) {
            throw new NotSupportedException();
        }

        public virtual void Dispose() {
        }

        public string Password { get; protected set; } = null;

        public bool IsPasswordRequired { get; protected set; } = false;

        public virtual bool IsPasswordCorrect => true;

        [CanBeNull]
        protected abstract string GetBaseId();

        [NotNull]
        protected abstract Task<IEnumerable<IFileInfo>> GetFileEntriesAsync();

        protected interface IFileInfo {
            string Filename { get; }

            [ItemNotNull]
            Task<byte[]> ReadAsync();
            
            Task CopyTo(string destination);
        }

        public virtual async Task<IReadOnlyList<AdditionalContentEntry>> GetEntriesAsync(IProgress<string> progress, CancellationToken cancellation) {
            var result = new List<AdditionalContentEntry>();
            var found = new List<string>();

            foreach (var fileInfo in await GetFileEntriesAsync()) {
                var filename = Path.GetFileName(fileInfo.Filename)?.ToLower();
                if (filename == null) continue;

                progress?.Report(filename);
                if (cancellation.IsCancellationRequested) break;

                AdditionalContentType type;
                string entryDirectory;

                switch (filename) {
                    case "ui_car.json": {
                        type = AdditionalContentType.Car;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "//ui_skin.json": {
                        // TODO (disabled atm)
                        // TODO: detect by preview.jpg & livery.png
                        type = AdditionalContentType.CarSkin;
                        entryDirectory = Path.GetDirectoryName(fileInfo.Filename);
                        break;
                    }

                    case "ui_track.json": {
                        type = AdditionalContentType.Track;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) {
                            directory = Path.GetDirectoryName(directory);
                        }
                        if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "ui_showroom.json": {
                        type = AdditionalContentType.Showroom;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), "ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    default:
                        continue;
                }

                if (entryDirectory == null) {
                    Logging.Warning("Entry directory is null: " + fileInfo.Filename);
                    continue;
                }

                if (found.Contains(entryDirectory)) continue;
                found.Add(entryDirectory);

                var id = entryDirectory != string.Empty ? Path.GetFileName(entryDirectory) : Path.GetFileNameWithoutExtension(GetBaseId() ?? "");
                if (string.IsNullOrEmpty(id)) {
                    Logging.Warning("ID is empty: " + fileInfo.Filename);
                    continue;
                }

                JObject jObject;
                try {
                    var jsonBytes = await fileInfo.ReadAsync();
                    jObject = JsonExtension.Parse(jsonBytes.GetString());
                } catch (Exception e) {
                    Logging.Warning("Can't read as a JSON (" + fileInfo.Filename + "): " + e);
                    continue;
                }

                result.Add(new AdditionalContentEntry(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
                        jObject.GetStringValueOnly("version")));
            }

            return result;
        }

        public virtual async Task InstallEntryToAsync(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory,
                IProgress<string> progress, CancellationToken cancellation) {
            foreach (var fileInfo in await GetFileEntriesAsync()) {
                var filename = fileInfo.Filename;
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

                    await fileInfo.CopyTo(path);
                }
            }
        }
    }
}