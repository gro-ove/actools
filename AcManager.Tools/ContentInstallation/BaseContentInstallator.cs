using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Types;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.ContentInstallation {
    internal abstract class BaseContentInstallator : IAdditionalContentInstallator {
        public virtual Task TrySetPasswordAsync(string password) {
            throw new NotSupportedException();
        }

        public virtual void Dispose() {}

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

        public virtual async Task<IReadOnlyList<ContentEntry>> GetEntriesAsync(IProgress<string> progress, CancellationToken cancellation) {
            var result = new List<ContentEntry>();
            var found = new List<string>();

            var list = (await GetFileEntriesAsync()).ToList();
            foreach (var fileInfo in list) {
                var name = Path.GetFileName(fileInfo.Filename)?.ToLower();
                if (name == null) continue;

                progress?.Report(name);
                if (cancellation.IsCancellationRequested) break;

                ContentType type;
                string entryDirectory;

                switch (name) {
                    case "ui_car.json": {
                        type = ContentType.Car;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "//ui_skin.json": {
                        // TODO (disabled atm)
                        // TODO: detect by preview.jpg & livery.png
                        type = ContentType.CarSkin;
                        entryDirectory = Path.GetDirectoryName(fileInfo.Filename);
                        break;
                    }

                    case "ui_track.json": {
                        type = ContentType.Track;
                        var directory = Path.GetDirectoryName(fileInfo.Filename);
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) {
                            directory = Path.GetDirectoryName(directory);
                        }
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "ui_showroom.json": {
                        type = ContentType.Showroom;
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

                result.Add(new ContentEntry(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
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
                            select new ContentEntry(ContentType.Font, bitmap.Filename, Path.GetFileName(filename)?.ToLower() ?? "",
                                    Path.GetFileName(withoutExtension)));

            return result;
        }

        public virtual async Task InstallEntryToAsync(ContentEntry entry, Func<string, bool> filter, string destination,
                IProgress<string> progress, CancellationToken cancellation) {
            var list = (await GetFileEntriesAsync()).ToList();

            if (entry.Type == ContentType.Car ||
                    entry.Type == ContentType.Track ||
                    entry.Type == ContentType.Showroom) {
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
            } else if (entry.Type == ContentType.CarSkin) {
                
            } else if (entry.Type == ContentType.Font) {
                var bitmapExtension = Path.GetExtension(entry.Path);

                var mainEntry = list.GetByPathOrDefault(entry.Path.ApartFromLast(bitmapExtension) + FontObject.FontExtension);
                await mainEntry.CopyTo(destination);

                var bitmapDestination = destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                await list.GetByPathOrDefault(entry.Path).CopyTo(bitmapDestination);
            } else {
                throw new ArgumentOutOfRangeException();
            }
        }
    }
}