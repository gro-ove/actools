using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    using IFileInfo = BaseAdditionalContentInstallator.IFileInfo;

    internal static class FileInfoEnumerableExtension {
        internal static IFileInfo GetByPathOrDefault(this IEnumerable<IFileInfo> source, string path) {
            return source.FirstOrDefault(x => string.Equals(x.Filename, path, StringComparison.OrdinalIgnoreCase));
        }
    }

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

        internal interface IFileInfo {
            string Filename { get; }

            [ItemNotNull]
            Task<byte[]> ReadAsync();
            
            Task CopyTo(string destination);
        }

        public virtual async Task<IReadOnlyList<AdditionalContentEntry>> GetEntriesAsync(IProgress<string> progress, CancellationToken cancellation) {
            var result = new List<AdditionalContentEntry>();
            var found = new List<string>();

            var list = (await GetFileEntriesAsync()).ToList();
            foreach (var fileInfo in list) {
                var name = Path.GetFileName(fileInfo.Filename)?.ToLower();
                if (name == null) continue;

                progress?.Report(name);
                if (cancellation.IsCancellationRequested) break;

                AdditionalContentType type;
                string entryDirectory;

                switch (name) {
                    case "ui_car.json": {
                        type = AdditionalContentType.Car;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
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
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) {
                            directory = Path.GetDirectoryName(directory);
                        }
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "ui_showroom.json": {
                        type = AdditionalContentType.Showroom;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
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
                    jObject = JsonExtension.Parse(jsonBytes.ToUtf8String());
                } catch (Exception e) {
                    Logging.Warning("Can’t read as a JSON (" + fileInfo.Filename + "): " + e);
                    continue;
                }

                result.Add(new AdditionalContentEntry(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
                        jObject.GetStringValueOnly("version")));
            }

            result.AddRange(from fileInfo in list
                            select fileInfo.Filename
                            into filename
                            let directory = Path.GetDirectoryName(filename)
                                    // only something in fonts/ or directly in root
                            where string.IsNullOrWhiteSpace(directory) ||
                                    String.Equals(Path.GetFileName(directory), @"fonts", StringComparison.OrdinalIgnoreCase)
                                    // only something ends on .txt
                            where filename.EndsWith(FontObject.FontExtension, StringComparison.OrdinalIgnoreCase)
                            let withoutExtension = filename.ApartFromLast(FontObject.FontExtension)
                                    // only something with a name which is a valid id
                            where AcStringValues.IsAppropriateId(Path.GetFileName(withoutExtension))
                            let bitmap = FontObject.BitmapExtensions.Select(x => list.GetByPathOrDefault(withoutExtension + x)).FirstOrDefault(x => x != null)
                                    // only something with a bitmap nearby
                            where bitmap != null
                            select new AdditionalContentEntry(AdditionalContentType.Font, bitmap.Filename, Path.GetFileName(filename)?.ToLower() ?? "",
                                    Path.GetFileName(withoutExtension)));

            return result;
        }

        public virtual async Task InstallEntryToAsync(AdditionalContentEntry entry, Func<string, bool> filter, string destination,
                IProgress<string> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync()).ToList();

            switch (entry.Type) {
                case AdditionalContentType.Car:
                case AdditionalContentType.Track:
                case AdditionalContentType.Showroom:
                    foreach (var fileInfo in list) {
                        var filename = fileInfo.Filename;
                        if (entry.Path != string.Empty && !FileUtils.IsAffected(entry.Path, filename)) continue;

                        var subFilename = filename.SubstringExt(entry.Path.Length);
                        if (subFilename.StartsWith(@"\")) subFilename = subFilename.Substring(1);

                        if (filter == null || filter(subFilename)) {
                            progress?.Report(subFilename);
                            if (cancellation.IsCancellationRequested) return;

                            var path = Path.Combine(destination, subFilename);
                            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? destination);

                            await fileInfo.CopyTo(path);
                        }
                    }

                    break;

                case AdditionalContentType.CarSkin:
                    break;

                case AdditionalContentType.Font:
                    var bitmapExtension = Path.GetExtension(entry.Path);

                    var mainEntry = list.GetByPathOrDefault(entry.Path.ApartFromLast(bitmapExtension) + FontObject.FontExtension);
                    await mainEntry.CopyTo(destination);

                    var bitmapDestination = destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                    await list.GetByPathOrDefault(entry.Path).CopyTo(bitmapDestination);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}