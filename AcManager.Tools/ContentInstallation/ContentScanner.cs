using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    internal static class ContentScanner {
        public class Scanned {
            public IReadOnlyList<ContentEntryBase> Result;
            public bool MissingContent;
            public Exception Exception;

            public Scanned(IReadOnlyList<ContentEntryBase> result, bool missingContent, Exception exception) {
                Result = result;
                MissingContent = missingContent;
                Exception = exception;
            }
        }

        private class FileNodeBase {
            [CanBeNull]
            public DirectoryNode Parent { get; }
            [CanBeNull]
            public string Key { get; }
            [CanBeNull]
            public string Name { get; }
            [CanBeNull]
            public string NameLowerCase { get; }

            public long Size { get; protected set; }

            public FileNodeBase(string key, DirectoryNode parent) {
                // ReSharper disable once VirtualMemberCallInConstructor
                Key = key;
                Name = Path.GetFileName(Key) ?? "";
                NameLowerCase = Name.ToLowerInvariant();
                Parent = parent;
            }
        }

        private class FileNode : FileNodeBase {
            public readonly IFileInfo Info;

            // File nodes always have non-null key and name
            [NotNull]
            public new DirectoryNode Parent => base.Parent ?? throw new Exception();
            [NotNull]
            public new string Key => base.Key ?? throw new Exception();
            [NotNull]
            public new string Name => base.Name ?? throw new Exception();
            [NotNull]
            public new string NameLowerCase => base.NameLowerCase ?? throw new Exception();

            public FileNode(IFileInfo info, DirectoryNode parent) : base(info.Key, parent) {
                Info = info;
                Size = info.Size;
            }

            [NotNull]
            public IEnumerable<FileNode> Siblings => Parent?.Files ?? new FileNode[0];
        }

        private class DirectoryNode : FileNodeBase {
            private readonly Dictionary<string, FileNode> _files = new Dictionary<string, FileNode>();
            private readonly Dictionary<string, DirectoryNode> _directories = new Dictionary<string, DirectoryNode>();

            public DirectoryNode(string key, DirectoryNode parent) : base(key, parent) { }

            public IEnumerable<FileNode> Files => _files.Values;
            public IEnumerable<DirectoryNode> Directories => _directories.Values;

            [CanBeNull]
            public FileNode GetSubFile(string name) {
                return _files.GetValueOrDefault(name.ToLowerInvariant());
            }

            [CanBeNull]
            public DirectoryNode GetSubDirectory(string name) {
                return _directories.GetValueOrDefault(name.ToLowerInvariant());
            }

            [CanBeNull]
            public bool HasSubFile(string name) {
                return _files.ContainsKey(name.ToLowerInvariant());
            }

            [CanBeNull]
            public bool HasSubDirectory(string name) {
                return _directories.ContainsKey(name.ToLowerInvariant());
            }

            #region Creating the tree
            private static void Split([NotNull] string relativePath, [CanBeNull] out string firstDirectory, [NotNull] out string anythingLeft) {
                if (string.IsNullOrEmpty(relativePath)) throw new Exception("Relative path is null or empty");

                if (relativePath[0] == Path.DirectorySeparatorChar ||
                        relativePath[0] == Path.AltDirectorySeparatorChar) {
                    relativePath = relativePath.Substring(1);
                }

                var index = relativePath.IndexOf(Path.DirectorySeparatorChar);
                var altIndex = relativePath.IndexOf(Path.AltDirectorySeparatorChar);

                if (index == -1 || altIndex != -1 && altIndex < index) {
                    index = altIndex;
                }

                if (index == -1) {
                    firstDirectory = null;
                    anythingLeft = relativePath;
                } else {
                    firstDirectory = relativePath.Substring(0, index);
                    anythingLeft = relativePath.Substring(index + 1);
                }
            }

            private void Add(IFileInfo file, string relativePath, bool top) {
                Split(relativePath, out string first, out string left);
                if (first == null) {
                    _files[left.ToLowerInvariant()] = new FileNode(file, this);
                    Size += file.Size;
                } else {
                    if (!_directories.TryGetValue(first, out DirectoryNode directory)) {
                        directory = new DirectoryNode(top ? first : Path.Combine(Key ?? "", first), this);
                        _directories[first.ToLowerInvariant()] = directory;
                    }

                    directory.Add(file, left, false);
                }
            }

            public void Add(IFileInfo file) {
                Add(file, file.Key, true);
            }
            #endregion
        }

        [ItemCanBeNull]
        private static async Task<byte[]> TryToReadData([CanBeNull] IFileInfo info) {
            return info == null ? null : await info.ReadAsync().ConfigureAwait(false);
        }

        [ItemNotNull]
        private static async Task<byte[]> ReadData(IFileInfo info) {
            var result = await info.ReadAsync().ConfigureAwait(false);
            if (result == null) {
                throw new MissingContentException();
            }

            return result;
        }

        [ItemCanBeNull]
        private static async Task<ContentEntryBase> CheckDirectoryNode(DirectoryNode directory, CancellationToken cancellation) {
            var ui = directory.GetSubDirectory("ui");
            if (ui != null) {
                // is it a car?
                var uiCar = ui.GetSubFile("ui_car.json");
                if (uiCar != null) {
                    var icon = await (ui.GetSubFile("badge.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    cancellation.ThrowIfCancellationRequested();

                    var data = await uiCar.Info.ReadAsync() ?? throw new MissingContentException();
                    var parsed = JsonExtension.Parse(data.ToUtf8String());
                    var carId = directory.Name ??
                            directory.GetSubDirectory("sfx")?.Files.Select(x => x.NameLowerCase)
                                     .FirstOrDefault(x => x.EndsWith(".bank") && x.Count('.') == 1 && x != "common.bank")?.ApartFromLast(".bank");
                    if (carId != null) {
                        return new CarContentEntry(directory.Key ?? "", carId,
                                parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
                    }
                }

                // is it a track? simple, without layouts
                var uiTrack = ui.GetSubFile("ui_track.json");
                if (uiTrack != null) {
                    var icon = await (ui.GetSubFile("outline.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    cancellation.ThrowIfCancellationRequested();

                    var data = await uiTrack.Info.ReadAsync() ?? throw new MissingContentException();
                    var parsed = JsonExtension.Parse(data.ToUtf8String());
                    var trackId = directory.Name ??
                            directory.Files.Where(x => x.NameLowerCase.EndsWith(".kn5")).OrderByDescending(x => x.Size)
                                     .FirstOrDefault()?.NameLowerCase.ApartFromLast(".kn5");
                    if (trackId != null) {
                        return new TrackContentEntry(directory.Key ?? "", trackId,
                                parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
                    }
                }

                // or is it a showroom?
                var uiShowroom = ui.GetSubFile("ui_showroom.json");
                if (uiShowroom != null) {
                    var icon = await (directory.GetSubFile("preview.jpg")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    cancellation.ThrowIfCancellationRequested();

                    var data = await uiShowroom.Info.ReadAsync() ?? throw new MissingContentException();
                    var parsed = JsonExtension.Parse(data.ToUtf8String());
                    var showroomId = directory.Name ??
                            directory.Files.Where(x => x.NameLowerCase.EndsWith(".kn5")).OrderByDescending(x => x.Info.Size)
                                     .FirstOrDefault()?.NameLowerCase.ApartFromLast(".kn5");
                    if (showroomId != null) {
                        return new ShowroomContentEntry(directory.Key ?? "", showroomId,
                                parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
                    }
                }
            }

            if (directory.HasSubFile("settings.ini")) {
                var kn5 = directory.Files.Where(x => x.NameLowerCase.EndsWith(".kn5")).ToList();
                var id = directory.Name;
                if (id != null) {
                    if (kn5.Any(x => x.NameLowerCase.ApartFromLast(".kn5") == directory.NameLowerCase)) {
                        var icon = await (directory.GetSubFile("preview.jpg")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                        cancellation.ThrowIfCancellationRequested();

                        return new ShowroomContentEntry(directory.Key ?? "", id,
                                AcStringValues.NameFromId(id), null, icon);
                    }
                }
            }

            var weatherIni = directory.GetSubFile("weather.ini");
            if (weatherIni != null) {
                var icon = await (directory.GetSubFile("preview.jpg")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                cancellation.ThrowIfCancellationRequested();

                var data = await weatherIni.Info.ReadAsync() ?? throw new MissingContentException();
                var parsed = IniFile.Parse(data.ToUtf8String());

                var name = parsed["LAUNCHER"].GetNonEmpty("NAME");
                if (name != null) {
                    var id = directory.Name ?? name;
                    return new WeatherContentEntry(directory.Key ?? "", id, name, icon);
                }
            }

            return null;
        }

        private static async Task<ContentEntryBase> CheckFileNode(FileNode file, CancellationToken cancellation) {
            if (file.Parent.NameLowerCase == "fonts" && file.NameLowerCase.EndsWith(FontObject.FontExtension)) {
                var id = file.NameLowerCase.ApartFromLast(FontObject.FontExtension);
                foreach (var bitmapExtension in FontObject.BitmapExtensions) {
                    var bitmap = file.Parent.GetSubFile(id + bitmapExtension);
                    if (bitmap != null) {
                        var fileData = await file.Info.ReadAsync();
                        cancellation.ThrowIfCancellationRequested();

                        var bitmapData = await bitmap.Info.ReadAsync();
                        cancellation.ThrowIfCancellationRequested();

                        if (fileData == null) throw new MissingContentException();

                        var icon = new FontObjectBitmap(bitmapData, fileData).GetIcon();

                        byte[] ToBytes(BitmapSource cropped) {
                            var encoder = new PngBitmapEncoder();
                            using (var stream = new MemoryStream()) {
                                encoder.Frames.Add(BitmapFrame.Create(cropped));
                                encoder.Save(stream);
                                return stream.ToArray();
                            }
                        }

                        return new FontContentEntry(bitmap.Key, file.NameLowerCase, id, ToBytes(icon));
                    }
                }
            }

            if (file.Parent.NameLowerCase == "ppfilters" && file.NameLowerCase.EndsWith(PpFilterObject.FileExtension)) {
                return new PpFilterContentEntry(file.Key, file.Name,
                        file.Name.ApartFromLast(PpFilterObject.FileExtension, StringComparison.OrdinalIgnoreCase));
            }

            if (file.Parent.NameLowerCase == "driver" && file.NameLowerCase.EndsWith(DriverModelObject.FileExtension)) {
                return new DriverModelContentEntry(file.Key, file.Name,
                        file.Name.ApartFromLast(DriverModelObject.FileExtension, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private class MissingContentException : Exception {}

        public static async Task<Scanned> GetEntriesAsync([NotNull] List<IFileInfo> list, string baseId,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            progress.Report(AsyncProgressEntry.FromStringIndetermitate("Scanning for content…"));

            var result = new List<ContentEntryBase>();
            var missingContent = false;
            Exception readException = null;

            var s = Stopwatch.StartNew();
            var root = new DirectoryNode(baseId, null);
            foreach (var info in list) {
                root.Add(info);
            }

            Logging.Debug($"Building tree: {s.Elapsed.TotalMilliseconds:F1} ms");

            s.Restart();
            var queue = new Queue<DirectoryNode>();
            queue.Enqueue(root);

            while (queue.Count > 0) {
                var directory = queue.Dequeue();

                ContentEntryBase found;
                try {
                    found = await CheckDirectoryNode(directory, cancellation);
                    if (cancellation.IsCancellationRequested) break;
                } catch (OperationCanceledException) {
                    break;
                } catch (MissingContentException) {
                    missingContent = true;
                    continue;
                } catch (Exception e) {
                    readException = e;
                    continue;
                }

                if (found != null) {
                    result.Add(found);
                } else {
                    foreach (var value in directory.Directories) {
                        queue.Enqueue(value);
                    }

                    foreach (var value in directory.Files) {
                        try {
                            found = await CheckFileNode(value, cancellation);
                            if (cancellation.IsCancellationRequested) break;
                        } catch (OperationCanceledException) {
                            break;
                        } catch (MissingContentException) {
                            missingContent = true;
                            continue;
                        } catch (Exception e) {
                            readException = e;
                            continue;
                        }

                        if (found != null) {
                            result.Add(found);
                        }
                    }
                }
            }

            Logging.Debug($"Scanning directories: {s.Elapsed.TotalMilliseconds:F1} ms");
            return new Scanned(result, missingContent, readException);
        }

        /*public static async Task<Scanned> GetEntriesFlatAsync([NotNull] List<IFileInfo> list, string baseId,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            Exception readException = null;
            var missingContent = false;

            async Task<byte[]> ReadData(IFileInfo fileInfo) {
                try {
                    var jsonBytes = await fileInfo.ReadAsync();
                    if (jsonBytes == null) {
                        missingContent = true;
                        return null;
                    }

                    return jsonBytes;
                } catch (Exception e) {
                    readException = e;
                    Logging.Warning(e);
                    return null;
                }
            }

            var result = new List<ContentEntryBase>();
            var found = new List<string>();

            for (var i = 0; i < list.Count; i++) {
                var fileInfo = list[i];
                var name = Path.GetFileName(fileInfo.Key)?.ToLower();
                if (name == null) continue;

                progress?.Report(name, i, list.Count);
                if (cancellation.IsCancellationRequested) break;

                ContentType type;
                string entryDirectory;
                IFileInfo iconEntry;

                switch (name) {
                    case "ui_car.json": {
                        type = ContentType.Car;
                        var directory = Path.GetDirectoryName(fileInfo.Key);
                        if (directory == null) continue;
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;

                        entryDirectory = Path.GetDirectoryName(directory);
                        iconEntry = list.GetByKey(Path.Combine(directory, "badge.png"));
                        break;
                    }

                    case "//ui_skin.json": {
                        // TODO (disabled atm)
                        // TODO: detect by preview.jpg & livery.png
                        type = ContentType.CarSkin;
                        entryDirectory = Path.GetDirectoryName(fileInfo.Key);
                        if (entryDirectory == null) continue;

                        iconEntry = list.GetByKey(Path.Combine(entryDirectory, "livery.png"));
                        break;
                    }

                    case "ui_track.json": {
                        type = ContentType.Track;

                        var directory = Path.GetDirectoryName(fileInfo.Key);
                        if (directory == null) continue;

                        iconEntry = list.GetByKey(Path.Combine(directory, "outline.png"));

                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) {
                            directory = Path.GetDirectoryName(directory);
                        }

                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }

                        entryDirectory = Path.GetDirectoryName(directory);
                        break;
                    }

                    case "ui_showroom.json": {
                        type = ContentType.Showroom;
                        var directory = Path.GetDirectoryName(fileInfo.Key);
                        if (!string.Equals(Path.GetFileName(directory), @"ui", StringComparison.OrdinalIgnoreCase)) continue;
                        entryDirectory = Path.GetDirectoryName(directory);
                        iconEntry = null;
                        break;
                    }

                    default:
                        continue;
                }

                if (entryDirectory == null) {
                    Logging.Warning("Entry directory is null: " + fileInfo.Key);
                    continue;
                }

                if (found.Contains(entryDirectory)) continue;
                found.Add(entryDirectory);

                var id = entryDirectory != string.Empty ? Path.GetFileName(entryDirectory) : Path.GetFileNameWithoutExtension(baseId ?? "");
                if (string.IsNullOrEmpty(id)) {
                    Logging.Warning("ID is empty: " + fileInfo.Key);
                    continue;
                }

                // we need to load icon before JSON-file — in case of solid archive, both of them
                // will be loaded on a second pass
                byte[] icon = null;
                if (iconEntry != null) {
                    icon = await ReadData(iconEntry);
                }

                var jsonBytes = await ReadData(fileInfo);
                if (jsonBytes == null) continue;

                var jObject = JsonExtension.Parse(jsonBytes.ToUtf8String());
                result.Add(new ContentEntryBase(type, entryDirectory, id.ToLower(), jObject.GetStringValueOnly("name"),
                        jObject.GetStringValueOnly("version"), icon));
            }

            result.AddRange(from fileInfo in list
                            select fileInfo.Key
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
                            let bitmap =
                                    FontObject.BitmapExtensions.Select(x => list.GetByPathOrDefault(withoutExtension + x)).FirstOrDefault(x => x != null)
                                    // only something with a bitmap nearby
                            where bitmap != null
                            select new ContentEntryBase(ContentType.Font, bitmap.Key, Path.GetFileName(filename)?.ToLower() ?? "",
                                    Path.GetFileName(withoutExtension)));

            return new Scanned(result, missingContent, readException);
        }*/
    }
}