using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    internal class ContentScanner {
        [NotNull]
        private readonly ContentInstallationParams _installationParams;

        public ContentScanner([NotNull] ContentInstallationParams installationParams) {
            _installationParams = installationParams ?? throw new ArgumentNullException(nameof(installationParams));
        }

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

        private class FileNodeBase : IWithId {
            [CanBeNull]
            public DirectoryNode Parent { get; }

            [CanBeNull]
            public string Key { get; }

            [CanBeNull]
            public string Name { get; private set; }

            [Localizable(false), CanBeNull]
            public string NameLowerCase { get; private set; }

            public long Size { get; protected set; }

            public FileNodeBase(string key, DirectoryNode parent) {
                // ReSharper disable once VirtualMemberCallInConstructor
                Key = parent == null ? null : key;

                try {
                    Name = Path.GetFileName(key);
                } catch (ArgumentException e) {
                    Logging.Warning(e);
                    throw new Exception($"Unsupported key: “{key}”");
                }

                NameLowerCase = Name?.ToLowerInvariant();
                Parent = parent;
            }

            public void ForceName([CanBeNull] string name) {
                Name = name;
                NameLowerCase = Name?.ToLowerInvariant();
            }

            public string Id => NameLowerCase;
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
            public IEnumerable<FileNode> Siblings => Parent.Files ?? new FileNode[0];

            [CanBeNull]
            public FileNode GetSibling(string name) {
                return Parent.GetSubFile(name);
            }
        }

        private class DirectoryNode : FileNodeBase {
            private readonly Dictionary<string, FileNode> _files = new Dictionary<string, FileNode>();
            private readonly Dictionary<string, DirectoryNode> _directories = new Dictionary<string, DirectoryNode>();

            public DirectoryNode(string key, DirectoryNode parent) : base(key, parent) { }

            public IEnumerable<FileNode> Files => _files.Values;
            public IEnumerable<DirectoryNode> Directories => _directories.Values;

            [CanBeNull]
            public FileNode GetSubFile([Localizable(false), NotNull] string name) {
                var index = name.IndexOfAny(new[] { '/', '\\' });
                if (index != -1) {
                    return GetSubDirectory(name.Substring(0, index))?.GetSubFile(name.Substring(index + 1));
                }

                return _files.GetValueOrDefault(name.ToLowerInvariant());
            }

            [CanBeNull]
            public DirectoryNode GetSubDirectory([Localizable(false), NotNull] string name) {
                var index = name.IndexOfAny(new[] { '/', '\\' });
                if (index != -1) {
                    return GetSubDirectory(name.Substring(0, index))?.GetSubDirectory(name.Substring(index + 1));
                }

                return _directories.GetValueOrDefault(name.ToLowerInvariant());
            }

            public bool HasSubFile([Localizable(false), NotNull] string name) {
                var index = name.IndexOfAny(new[] { '/', '\\' });
                if (index != -1) {
                    return GetSubDirectory(name.Substring(0, index))?.HasSubFile(name.Substring(index + 1)) == true;
                }

                return _files.ContainsKey(name.ToLowerInvariant());
            }

            public bool HasSubDirectory([Localizable(false), NotNull] string name) {
                var index = name.IndexOfAny(new[] { '/', '\\' });
                if (index != -1) {
                    return GetSubDirectory(name.Substring(0, index))?.HasSubDirectory(name.Substring(index + 1)) == true;
                }

                return _directories.ContainsKey(name.ToLowerInvariant());
            }

            public bool HasAnySubDirectory([Localizable(false), NotNull] params string[] name) {
                return name.Any(HasSubDirectory);
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
                    var firstKey = first.ToLowerInvariant();
                    if (!_directories.TryGetValue(firstKey, out DirectoryNode directory)) {
                        directory = new DirectoryNode(top ? first : Path.Combine(Key ?? "", first), this);
                        _directories[firstKey] = directory;
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

        // Because of AC layouts, it’s the most difficult bit of guessing. Basic track without layouts,
        // track with layouts, extra layout for a multi-layout track, basic track for a multi-layout track (as a layout),
        // extra layout for a basic track…
        private async Task<ContentEntryBase> CheckDirectoryNodeForTrack(DirectoryNode directory, CancellationToken cancellation) {
            var ui = directory.GetSubDirectory("ui");
            if (ui == null) return null;

            Logging.Write("Candidate to be a track: " + directory.Key);

            // First of all, let’s find out if it’s a track at all
            var uiTrack = ui.GetSubFile("ui_track.json");
            var uiTrackSubs = ui.Directories.Select(x => x.GetSubFile("ui_track.json")).NonNull().ToList();

            if (uiTrack == null && uiTrackSubs.Count == 0) {
                // It’s not a track
                Logging.Write("Not a track");
                return null;
            }

            // INI-files with modes
            var iniTrack = uiTrack == null ? null : directory.GetSubFile("models.ini");
            var iniTrackSubs = new List<FileNode>();
            for (var i = uiTrackSubs.Count - 1; i >= 0; i--) {
                var layoutName = uiTrackSubs[i].Parent.NameLowerCase;
                var models = directory.GetSubFile($"models_{layoutName}.ini");
                if (models == null) {
                    uiTrackSubs.RemoveAt(i);
                } else {
                    iniTrackSubs.Add(models);
                }
            }

            // Let’s get missing content stuff out of the way (we’ll still keep throwing that exception
            // later if needed, this piece is just to ensure all required files are asked for in the first pass)
            var missingContent = uiTrack?.Info.IsAvailable() == false | iniTrack?.Info.IsAvailable() == false |
                    uiTrackSubs.Aggregate(false, (v, x) => v | !x.Info.IsAvailable()) |
                    iniTrackSubs.Aggregate(false, (v, x) => v | !x.Info.IsAvailable());
            if (missingContent) {
                // And, if it’s just a first step, let’s ask for outlines as well
                foreach (var node in uiTrackSubs.Append(uiTrack).NonNull()) {
                    node.GetSibling("outline.png")?.Info.IsAvailable();
                }

                Logging.Write("Missing content…");
                throw new MissingContentException();
            }

            // It’s a track, let’s find out layout IDs
            // var layoutLowerCaseIds = uiTrackSubs.Select(x => x.Parent.NameLowerCase).ToList();

            // And track ID (so far, without layouts)
            var trackId = directory.Name;
            if (trackId == null) {
                Logging.Write("Directory’s name is null, let’s try to guess track’s ID");

                if (iniTrack != null || iniTrackSubs.Count > 0) {
                    // Looks like KN5 are referenced via ini-files, we can’t rely on KN5 name to determine
                    // missing track ID

                    // UPD: Let’s try anyway, by looking for the biggest referenced KN5-file with an unusual name
                    Logging.Debug("CAN’T FOUND PROPER TRACK ID NOWHERE! LET’S TRY TO GUESS…");

                    bool IsNameUnusual(string name) {
                        var n = name.ToLowerInvariant().ApartFromLast(".kn5");

                        if (n.Length < 5) {
                            // Could be some sort of shortening.
                            return false;
                        }

                        if (n.Contains(" ")) {
                            // It might be the right name, but it’s unlikely.
                            return false;
                        }

                        if (double.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)) {
                            // Numbers: 0.kn5, 10.kn5, …
                            // Kunos name their extra files like that.
                            return false;
                        }

                        var marks = "drift|circuit|sprint|race|trackday|full|layout|start|trees|grass|normal|reverse|chicane|oval|wet|dtm|gp|pit";
                        if (Regex.IsMatch(n, $@"^(?:{marks})(?![a-z])|(?<![a-z])(?:{marks})$")) {
                            // Might look like some extra layouts.
                            return false;
                        }

                        return true;
                    }

                    var potentialId = (await iniTrackSubs.Prepend(iniTrack).NonNull().Select(x => x.Info.ReadAsync()).WhenAll())
                            .SelectMany(x => TrackContentEntry.GetLayoutModelsNames(IniFile.Parse(x.ToUtf8String())).ToList())
                            .Distinct().Where(IsNameUnusual).OrderByDescending(x => directory.GetSubFile(x)?.Info.Size)
                            .FirstOrDefault()?.ToLowerInvariant().ApartFromLast(".kn5");
                    if (potentialId != null) {
                        trackId = potentialId;
                    } else {
                        Logging.Write("Can’t determine ID because of ini-files");
                        return null;
                    }
                } else {
                    trackId = directory.Files.Where(x => x.NameLowerCase.EndsWith(".kn5")).OrderByDescending(x => x.Size)
                                       .FirstOrDefault()?.NameLowerCase.ApartFromLast(".kn5");
                    if (trackId == null) {
                        Logging.Write("Can’t determine ID");
                        return null;
                    }
                }

                Logging.Write("Guessed ID: " + trackId);
            }

            Logging.Write("Track ID: " + directory.Name);

            // Some functions
            async Task<Tuple<string, string, byte[]>> LoadNameVersionIcon(FileNode uiFile) {
                var icon = await (uiFile.GetSibling("outline.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                var data = await uiFile.Info.ReadAsync() ?? throw new MissingContentException();
                var parsed = JsonExtension.Parse(data.ToUtf8String());
                cancellation.ThrowIfCancellationRequested();
                return Tuple.Create(parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
            }

            // Tuple: (models in array; required, but missing models)
            async Task<Tuple<List<string>, List<string>>> LoadModelsIni(FileNode node) {
                var data = node == null ? null : await node.Info.ReadAsync();
                cancellation.ThrowIfCancellationRequested();
                if (data == null) return null;

                var names = TrackContentEntry.GetModelsNames(IniFile.Parse(data.ToUtf8String())).ToList();
                var existing = names.Where(directory.HasSubFile).ToList();
                return Tuple.Create(existing, names.ApartFrom(existing).ToList());
            }

            Task<Tuple<List<string>, List<string>>> LoadMainModelsIni() {
                if (iniTrack != null) {
                    return LoadModelsIni(iniTrack);
                }

                var name = $@"{trackId}.kn5";
                return Task.FromResult(directory.HasSubFile(name) ?
                        Tuple.Create(new List<string> { name }, new List<string>(0)) :
                        Tuple.Create(new List<string>(0), new List<string> { name }));
            }

            if (uiTrack != null && uiTrackSubs.Count == 0) {
                // It’s a basic track, no layouts
                Logging.Write("Basic type of track");

                var nvi = await LoadNameVersionIcon(uiTrack);
                var models = await LoadMainModelsIni();
                return await TrackContentEntry.Create(directory.Key ?? "", trackId, models.Item1, models.Item2,
                        nvi.Item1, nvi.Item2, nvi.Item3);
            }

            // Let’s prepare layouts
            if (uiTrackSubs.Count == 0) {
                Logging.Write("Layouts not found");
                return null;
            }

            // It’s a basic track, no layouts
            Logging.Write("Layouts");

            var layouts = new List<TrackContentLayoutEntry>(uiTrackSubs.Count);
            for (var i = 0; i < uiTrackSubs.Count; i++) {
                var sub = uiTrackSubs[i];
                var nvi = await LoadNameVersionIcon(sub);
                var models = await LoadModelsIni(iniTrackSubs[i]);
                layouts.Add(new TrackContentLayoutEntry(sub.Parent.Name ?? "-", models.Item1, models.Item2,
                        nvi.Item1, nvi.Item2, nvi.Item3));
            }

            if (uiTrack != null) {
                var nvi = await LoadNameVersionIcon(uiTrack);
                var models = await LoadMainModelsIni();
                layouts.Add(new TrackContentLayoutEntry("", models.Item1, models.Item2,
                        nvi.Item1, nvi.Item2, nvi.Item3));
            }

            return await TrackContentEntry.Create(directory.Key ?? "", trackId, layouts);
        }

        [ItemCanBeNull]
        private async Task<ContentEntryBase> CheckDirectoryNode(DirectoryNode directory, CancellationToken cancellation) {
            if (directory.Parent?.NameLowerCase == "python" && directory.Parent.Parent?.NameLowerCase == "apps" ||
                    directory.HasSubFile(directory.Name + ".py")) {
                var id = directory.Name;
                if (id == null) {
                    // It’s unlikely there will be a car or a track in apps/python directory
                    return null;
                }

                // App?
                var root = directory.Parent?.Parent?.Parent;
                var gui = root?.GetSubDirectory("content")?.GetSubDirectory("gui")?.GetSubDirectory("icons");

                // Collecting values…
                var missing = false;
                var uiAppFound = false;
                string version = null, name = null;

                // Maybe, it’s done nicely?
                var uiApp = directory.GetSubDirectory("ui")?.GetSubFile("ui_app.json");
                if (uiApp != null) {
                    uiAppFound = true;
                    var data = await uiApp.Info.ReadAsync();
                    if (data == null) {
                        missing = true;
                    } else {
                        var parsed = JsonExtension.Parse(data.ToUtf8String());
                        name = parsed.GetStringValueOnly("name");
                        version = parsed.GetStringValueOnly("version");
                    }
                }

                // Let’s try to guess version
                if (version == null && !uiAppFound) {
                    foreach (var c in PythonAppObject.VersionSources.Select(directory.GetSubFile).NonNull()) {
                        var r = await c.Info.ReadAsync();
                        if (r == null) {
                            missing = true;
                        } else {
                            version = PythonAppObject.GetVersion(r.ToUtf8String());
                            if (version != null) break;
                        }
                    }
                }

                // And icon
                byte[] icon;
                List<FileNode> icons;

                if (gui != null) {
                    icons = gui.Files.Where(x => x.NameLowerCase.EndsWith("_on.png") || x.NameLowerCase.EndsWith("_off.png")).ToList();
                    var mainIcon = icons.GetByIdOrDefault(directory.NameLowerCase + "_off.png") ??
                            icons.OrderByDescending(x => x.NameLowerCase.Length).FirstOrDefault();

                    icon = await (mainIcon?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    if (mainIcon != null && icon == null) {
                        missing = true;
                    }

                    cancellation.ThrowIfCancellationRequested();
                } else {
                    icon = null;
                    icons = null;
                }

                if (missing) {
                    throw new MissingContentException();
                }

                return new PythonAppContentEntry(directory.Key ?? "", id,
                        name ?? id, version, icon, icons?.Select(x => x.Key));
            }

            var ui = directory.GetSubDirectory("ui");
            if (ui != null) {
                // Is it a car?
                var uiCar = ui.GetSubFile("ui_car.json");
                if (uiCar != null) {
                    var icon = await (ui.GetSubFile("badge.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    cancellation.ThrowIfCancellationRequested();

                    var data = await uiCar.Info.ReadAsync() ?? throw new MissingContentException();
                    var parsed = JsonExtension.Parse(data.ToUtf8String());
                    var carId = directory.Name ??
                            directory.GetSubDirectory("sfx")?.Files.Select(x => x.NameLowerCase)
                                     .FirstOrDefault(x => x.EndsWith(@".bank") && x.Count('.') == 1 && x != @"common.bank")?.ApartFromLast(@".bank");

                    if (carId != null) {
                        return new CarContentEntry(directory.Key ?? "", carId, parsed.GetStringValueOnly("parent") != null,
                                parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
                    }
                }

                // A track?
                var foundTrack = await CheckDirectoryNodeForTrack(directory, cancellation);
                if (foundTrack != null) {
                    return foundTrack;
                }

                // Or maybe a showroom?
                var uiShowroom = ui.GetSubFile(@"ui_showroom.json");
                if (uiShowroom != null) {
                    var icon = await (directory.GetSubFile(@"preview.jpg")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                    cancellation.ThrowIfCancellationRequested();

                    var data = await uiShowroom.Info.ReadAsync() ?? throw new MissingContentException();
                    var parsed = JsonExtension.Parse(data.ToUtf8String());
                    var showroomId = directory.Name ??
                            directory.Files.Where(x => x.NameLowerCase.EndsWith(@".kn5")).OrderByDescending(x => x.Info.Size)
                                     .FirstOrDefault()?.NameLowerCase.ApartFromLast(@".kn5");
                    if (showroomId != null) {
                        return new ShowroomContentEntry(directory.Key ?? "", showroomId,
                                parsed.GetStringValueOnly("name"), parsed.GetStringValueOnly("version"), icon);
                    }
                }
            } else {
                // Another case for showrooms
                if (directory.Name != null
                        && directory.HasSubFile(directory.Name + @".kn5")
                        && directory.HasSubFile(@"colorCurves.ini") && directory.HasSubFile(@"ppeffects.ini")) {
                    var icon = directory.HasSubFile(@"preview.jpg")
                            ? await (directory.GetSubFile(@"preview.jpg")?.Info.ReadAsync() ?? throw new MissingContentException())
                            : null;
                    cancellation.ThrowIfCancellationRequested();
                    return new ShowroomContentEntry(directory.Key ?? "", directory.Name ?? throw new ArgumentException(), iconData: icon);
                }
            }

            var uiTrackSkin = directory.GetSubFile("ui_track_skin.json");
            if (uiTrackSkin != null && TracksManager.Instance != null) {
                var icon = await (directory.GetSubFile("preview.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                cancellation.ThrowIfCancellationRequested();

                var data = await uiTrackSkin.Info.ReadAsync() ?? throw new MissingContentException();
                var parsed = JsonExtension.Parse(data.ToUtf8String());
                var skinId = parsed.GetStringValueOnly("id") ?? directory.Name;
                var trackId = parsed.GetStringValueOnly("track");
                var name = parsed.GetStringValueOnly("name");

                if (skinId != null && trackId != null) {
                    return new TrackSkinContentEntry(directory.Key ?? "", skinId, trackId, name,
                            parsed.GetStringValueOnly("version"), icon);
                }
            }

            if (directory.HasSubFile("settings.ini")) {
                var kn5 = directory.Files.Where(x => x.NameLowerCase.EndsWith(@".kn5")).ToList();
                var id = directory.Name;
                if (id != null) {
                    if (kn5.Any(x => x.NameLowerCase.ApartFromLast(@".kn5") == directory.NameLowerCase)) {
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

            var uiCarSkin = directory.GetSubFile("ui_skin.json");
            if ((uiCarSkin != null || directory.HasSubFile("preview.jpg") && directory.HasSubFile("livery.png"))
                    && CarsManager.Instance != null /* for crawlers only */) {
                var icon = await (directory.GetSubFile("livery.png")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                cancellation.ThrowIfCancellationRequested();

                string carId;
                var skinFor = await (directory.GetSubFile("cm_skin_for.json")?.Info.ReadAsync() ?? Task.FromResult((byte[])null));
                if (skinFor != null) {
                    carId = JsonExtension.Parse(skinFor.ToUtf8String())[@"id"]?.ToString();
                } else {
                    carId = _installationParams.CarId;

                    if (carId == null && directory.Parent?.NameLowerCase == "skins") {
                        carId = directory.Parent.Parent?.Name;
                    }

                    if (carId == null) {
                        carId = AcContext.Instance.CurrentCar?.Id;
                    }
                }

                if (carId == null) {
                    throw new Exception("Can’t figure out car’s ID");
                }

                var skinId = directory.Name;
                if (skinId != null) {
                    string name;
                    if (uiCarSkin != null) {
                        var data = await uiCarSkin.Info.ReadAsync() ?? throw new MissingContentException();
                        var parsed = JsonExtension.Parse(data.ToUtf8String());
                        name = parsed.GetStringValueOnly("name");
                    } else {
                        name = AcStringValues.NameFromId(skinId);
                    }

                    return new CarSkinContentEntry(directory.Key ?? "", skinId, carId, name, icon);
                }
            }

            // New textures
            if (directory.NameLowerCase == "damage" && directory.HasSubFile("flatspot_fl.png")) {
                return new TexturesConfigEntry(directory.Key ?? "", directory.Name ?? @"damage");
            }

            if (directory.Parent?.NameLowerCase == "crew_brand" && directory.HasSubFile("Brands_Crew.dds") && directory.HasSubFile("Brands_Crew.jpg")
                    && directory.HasSubFile("Brands_Crew_NM.dds")) {
                return new CrewBrandEntry(directory.Key ?? "", directory.Name ?? @"unknown");
            }

            if (directory.Parent?.NameLowerCase == "crew_helmet" && directory.HasSubFile("Crew_HELMET_Color.dds")) {
                return new CrewHelmetEntry(directory.Key ?? "", directory.Name ?? @"unknown");
            }

            // TODO: More driver and crew textures

            if (directory.NameLowerCase == "clouds" && directory.Files.Any(
                    x => (x.NameLowerCase.StartsWith(@"cloud") || directory.Parent?.NameLowerCase == "texture") && x.NameLowerCase.EndsWith(@".dds"))) {
                return new TexturesConfigEntry(directory.Key ?? "", directory.Name ?? @"clouds");
            }

            if (directory.NameLowerCase == "clouds_shared" && directory.Files.Any(
                    x => (x.NameLowerCase.StartsWith(@"cloud") || directory.Parent?.NameLowerCase == "texture") && x.NameLowerCase.EndsWith(@".dds"))) {
                return new TexturesConfigEntry(directory.Key ?? "", directory.Name ?? @"clouds_shared");
            }

            if (directory.NameLowerCase == "people" && (directory.HasSubFile("crowd_sit.dds") || directory.HasSubFile("people_sit.dds"))) {
                return new TexturesConfigEntry(directory.Key ?? "", directory.Name ?? @"people");
            }

            if (directory.HasSubFile("dwrite.dll") && directory.HasSubDirectory("extension")) {
                var dwrite = directory.GetSubFile("dwrite.dll");
                var extension = directory.GetSubDirectory("extension");
                var description = directory.GetSubFile("description.jsgme");
                string version;
                if (description != null) {
                    var data = await description.Info.ReadAsync() ?? throw new MissingContentException();
                    version = Regex.Match(data.ToUtf8String(), @"(?<=v)\d.*").Value?.TrimEnd('.').Or(null);
                } else {
                    var parent = directory;
                    while (parent.Parent?.Name != null) parent = parent.Parent;
                    version = parent.Name != null ? Regex.Match(parent.Name, @"(?<=v)\d.*").Value?.TrimEnd('.').Or(null) : null;
                }

                return new ShadersPatchEntry(directory.Key ?? "", new[]{ dwrite.Key, extension.Key }, version);
            }

            if (directory.NameLowerCase == "__gbwsuite") {
                return new CustomFolderEntry(directory.Key ?? "", new[]{ directory.Key }, "GBW scripts", "__gbwSuite");
            }

            if (directory.HasSubFile("weather.lua") && directory.Parent.NameLowerCase == "weather") {
                return new CustomFolderEntry(directory.Key ?? "", new[]{ directory.Key }, $"Weather FX script “{AcStringValues.NameFromId(directory.Name)}”",
                        Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "weather", directory.Name), 1e5);
            }

            if (directory.HasSubFile("controller.lua") && directory.Parent.NameLowerCase == "weather-controllers") {
                return new CustomFolderEntry(directory.Key ?? "", new[]{ directory.Key }, $"Weather FX controller “{AcStringValues.NameFromId(directory.Name)}”",
                        Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "weather-controllers", directory.Name), 1e5);
            }

            // Mod
            if (directory.Parent?.NameLowerCase == "mods"
                    && (directory.HasAnySubDirectory("content", "apps", "system", "launcher", "extension") || directory.HasSubFile("dwrite.dll"))) {
                var name = directory.Name;
                if (name != null && directory.GetSubDirectory("content")?.GetSubDirectory("tracks")?.Directories.Any(
                        x => x.GetSubDirectory("skins")?.GetSubDirectory("default")?.GetSubFile("ui_track_skin.json") != null) != true) {
                    var description = directory.Files.FirstOrDefault(x => x.NameLowerCase.EndsWith(@".jsgme"));
                    if (description == null && directory.HasSubDirectory("documentation")) {
                        description = directory.GetSubDirectory("documentation")?.Files.FirstOrDefault(x => x.NameLowerCase.EndsWith(@".jsgme"));
                    }

                    if (description != null) {
                        var data = await description.Info.ReadAsync() ?? throw new MissingContentException();
                        return new GenericModConfigEntry(directory.Key ?? "", name, data.ToUtf8String());
                    }

                    return new GenericModConfigEntry(directory.Key ?? "", name);
                }
            }

            return null;
        }

        private async Task<ContentEntryBase> CheckFileNode(FileNode file, CancellationToken cancellation) {
            if (file.Parent.NameLowerCase == "fonts") {
                if (file.NameLowerCase.EndsWith(FontObject.FontExtension)) {
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
                } else if (file.NameLowerCase.EndsWith(TrueTypeFontObject.FileExtension)) {
                    return new TrueTypeFontContentEntry(file.Key, file.Name,
                            file.Name.ApartFromLast(TrueTypeFontObject.FileExtension, StringComparison.OrdinalIgnoreCase));
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

            if (file.NameLowerCase.EndsWith(@".xaml")) {
                var data = await file.Info.ReadAsync();
                if (data == null) {
                    throw new MissingContentException();
                }

                var version = CmThemeEntry.GetVersion(data.ToUtf8String(), out var isTheme);
                return isTheme ? new CmThemeEntry(file.Key, file.Name, version) : null;
            }

            // A system config file?
            if (file.NameLowerCase.EndsWith(@".ini") && file.Parent.NameLowerCase == "cfg" &&
                    file.Parent.Parent?.NameLowerCase == "system") {
                switch (file.NameLowerCase) {
                    case "audio_engine.ini":
                    case "damage_displayer.ini":
                    case "graphics.ini":
                    case "hdr.ini":
                    case "pitstop.ini":
                    case "skidmarks.ini":
                    case "tyre_smoke.ini":
                    case "tyre_smoke_grass.ini":
                    case "tyre_pieces_grass.ini":
                    case "vr.ini":
                        return new SystemConfigEntry(file.Key, file.Name);
                }
            }

            if (file.NameLowerCase == ReshadeSetupEntry.ReshadeFileName && file.Parent.HasSubFile(ReshadeSetupEntry.ReshadeConfigFileName)) {
                var reshadeEntry = await GetReshadeEntry();
                if (reshadeEntry != null) {
                    return reshadeEntry;
                }

                async Task<ContentEntryBase> GetReshadeEntry() {
                    var root = file.Parent;
                    var configuration = root.GetSubFile(ReshadeSetupEntry.ReshadeConfigFileName);
                    if (configuration == null) return null;

                    var data = await configuration.Info.ReadAsync();
                    if (data == null) throw new MissingContentException();

                    var ini = IniFile.Parse(data.ToUtf8String())["GENERAL"];
                    var presets = ini.GetNonEmpty("PresetFiles")?.Split(',').Select(ToRelativePath).NonNull().ToList();
                    if (presets == null || presets.Count == 0) return null;

                    var resources = ini.GetNonEmpty("EffectSearchPaths")?.Split(',')
                                       .Concat(ini.GetNonEmpty("TextureSearchPaths")?.Split(',') ?? new string[0])
                                       .Select(ToRelativePath).NonNull().ToList() ?? new List<string>();

                    return new ReshadeSetupEntry(file.Key, presets.JoinToReadableString(), presets.Concat(resources));

                    string ToRelativePath(string input) {
                        if (!Path.IsPathRooted(input)) return input;

                        while (input != null) {
                            var index = input.IndexOfAny(new[] { '/', '\\' });
                            if (index == -1) return null;

                            input = input.Substring(index + 1);
                            if (root.HasSubFile(input)) return input;
                        }

                        return null;
                    }
                }
            }

            return null;
        }

        private class MissingContentException : Exception { }

        public async Task<Scanned> GetEntriesAsync([NotNull] List<IFileInfo> list, string baseId, string baseName,
                [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Scanning…"));

            var result = new List<ContentEntryBase>();
            var missingContent = false;
            Exception readException = null;

            var s = Stopwatch.StartNew();
            var root = new DirectoryNode(_installationParams.FallbackId ?? baseId, null);
            root.ForceName(baseName);

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
                    found = await CheckDirectoryNode(directory, cancellation).ConfigureAwait(false); // WHY IT DOES NOT WORK?
                    if (cancellation.IsCancellationRequested) break;
                } catch (Exception e) when (e.IsCancelled()) {
                    break;
                } catch (MissingContentException) {
                    missingContent = true;
                    continue;
                } catch (Exception e) {
                    Logging.Warning(e);
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
                            found = await CheckFileNode(value, cancellation).ConfigureAwait(false);
                            if (cancellation.IsCancellationRequested) break;
                        } catch (Exception e) when (e.IsCancelled()) {
                            break;
                        } catch (MissingContentException) {
                            missingContent = true;
                            continue;
                        } catch (Exception e) {
                            Logging.Warning(e);
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
    }
}