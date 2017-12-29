using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class CarContentEntry : ContentEntryBase<CarObject> {
        private readonly bool _isChild;

        public CarContentEntry([NotNull] string path, [NotNull] string id, bool isChild, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) {
            _isChild = isChild;
        }

        public override double Priority => _isChild ? 50d : 51d;

        public override string GenericModTypeName => "Car";
        public override string NewFormat => ToolsStrings.ContentInstallation_CarNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_CarExisting;

        public override FileAcManager<CarObject> GetManager() {
            return CarsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (!x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json"));
            }

            bool PreviewsFilter(string x) {
                return !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepSkinsPreviews) { Filter = PreviewsFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformationAndSkinsPreviews) { Filter = x => UiFilter(x) && PreviewsFilter(x) }
            });
        }
    }

    public sealed class TrackContentLayoutEntry : NotifyPropertyChanged, IWithId {
        /// <summary>
        /// KN5-files referenced in assigned models.ini if exists.
        /// </summary>
        [CanBeNull]
        public readonly List<string> Kn5Files;

        public string DisplayKn5Files => Kn5Files?.JoinToReadableString();

        // Similar to Kn5Files, but here is a list of files required, but not provided in the source.
        [CanBeNull]
        public readonly List<string> RequiredKn5Files;

        private string[] _missingKn5Files = new string[0];

        public string[] MissingKn5Files {
            get => _missingKn5Files;
            set {
                value = value ?? new string[0];
                if (Equals(value, _missingKn5Files)) return;
                _missingKn5Files = value;
                DisplayMissingKn5Files = value.JoinToReadableString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMissingKn5Files));
            }
        }

        public string DisplayMissingKn5Files { get; private set; }

        /// <summary>
        /// If it’s not an actual layout, but instead just a base-track in a multi-layout situation, Id is empty!
        /// </summary>
        [NotNull]
        public string Id { get; }

        private bool _active = true;

        public bool Active {
            get => _active;
            set {
                if (Equals(value, _active)) return;
                _active = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public byte[] IconData { get; }

        public TrackContentLayoutEntry([NotNull] string id, [CanBeNull] List<string> kn5Files, [CanBeNull] List<string> requiredKn5Files,
                string name = null, string version = null, byte[] iconData = null) {
            Kn5Files = kn5Files;
            RequiredKn5Files = requiredKn5Files;
            Id = id;
            Name = name;
            Version = version;
            IconData = iconData;
        }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        public string DisplayName => ExistingLayout == null ? $"{Name} (new layout)" :
                Name == ExistingLayout.LayoutName ? $"{Name} (update for layout)" : $"{Name} (update for {ExistingLayout.LayoutName})";

        private TrackObjectBase _existingLayout;

        public TrackObjectBase ExistingLayout {
            get => _existingLayout;
            set {
                if (Equals(value, _existingLayout)) return;
                _existingLayout = value;
                OnPropertyChanged();
            }
        }

        private BetterImage.BitmapEntry? _icon;
        public BetterImage.BitmapEntry? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));
    }

    public class TrackContentEntry : ContentEntryBase<TrackObject> {
        // In case there are no extra layouts, but models.ini, here will be stored list of referenced KN5 files
        [CanBeNull]
        public readonly List<string> Kn5Files;

        // Similar to Kn5Files, but here is a list of files required, but not provided in the source.
        [CanBeNull]
        public readonly List<string> RequiredKn5Files;

        private string[] _missingKn5Files = new string[0];

        public string[] MissingKn5Files {
            get => _missingKn5Files;
            set {
                value = value ?? new string[0];
                if (Equals(value, _missingKn5Files)) return;
                _missingKn5Files = value;
                DisplayMissingKn5Files = value.JoinToReadableString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMissingKn5Files));
            }
        }

        public string DisplayMissingKn5Files { get; private set; }

        // Layouts!
        [CanBeNull]
        public IReadOnlyList<TrackContentLayoutEntry> Layouts { get; }

        private static string GetName([CanBeNull] IReadOnlyList<TrackContentLayoutEntry> layouts) {
            if (layouts == null) return null;
            return TrackObject.FindNameForMultiLayoutMode(layouts.Select(x => x.Name).ToList()) ??
                    layouts.FirstOrDefault()?.Name;
        }

        public override double Priority => 90d;

        private TrackContentEntry([NotNull] string path, [NotNull] string id, [CanBeNull] List<string> kn5Files,
                [CanBeNull] List<string> requiredKn5Files, string name = null, string version = null,
                byte[] iconData = null) : base(path, id, name, version, iconData) {
            RequiredKn5Files = requiredKn5Files;
            Kn5Files = kn5Files;
        }

        private TrackContentEntry([NotNull] string path, [NotNull] string id, [NotNull] IReadOnlyList<TrackContentLayoutEntry> layouts)
                : base(path, id, GetName(layouts), layouts.FirstOrDefault()?.Version) {
            Layouts = layouts.ToList();
            foreach (var layout in Layouts) {
                layout.PropertyChanged += OnLayoutPropertyChanged;
            }
        }

        public static async Task<TrackContentEntry> Create([NotNull] string path, [NotNull] string id, [CanBeNull] List<string> kn5Files,
                [CanBeNull] List<string> requiredKn5Files, string name = null, string version = null, byte[] iconData = null) {
            var result = new TrackContentEntry(path, id, kn5Files, requiredKn5Files, name, version, iconData);
            await result.Initialize().ConfigureAwait(false);
            return result;
        }

        public static async Task<TrackContentEntry> Create([NotNull] string path, [NotNull] string id, [NotNull] IReadOnlyList<TrackContentLayoutEntry> layouts) {
            var result = new TrackContentEntry(path, id, layouts);
            await result.Initialize().ConfigureAwait(false);
            return result;
        }

        public static IEnumerable<string> GetLayoutModelsNames(IniFile file) {
            return file.GetSections("MODEL").Select(x => x.GetNonEmpty("FILE")).NonNull();
        }

        public static IEnumerable<string> GetModelsNames(IniFile file) {
            return file.GetSections("MODEL").Concat(file.GetSections("DYNAMIC_OBJECT")).Select(x => x.GetNonEmpty("FILE")).NonNull();
        }

        private TrackObjectBase _noLayoutsExistingLayout;

        public TrackObjectBase NoLayoutsExistingLayout {
            get => _noLayoutsExistingLayout;
            set {
                if (Equals(value, _noLayoutsExistingLayout)) return;
                _noLayoutsExistingLayout = value;
                OnPropertyChanged();
            }
        }

        private async Task Initialize() {
            TrackObject existing;
            try {
                existing = await GetExistingAcObjectAsync();
            } catch (Exception) {
                // Specially for LINQPad scripts
                return;
            }

            if (existing == null) return;

            // Second part of that track trickery. Now, we have to offer user to combine existing and new track
            // in a lot of various ways

            var existingName = existing.DisplayNameWithoutCount;
            var existingModels = ((IEnumerable<TrackObjectBase>)existing.MultiLayouts ?? new TrackObjectBase[] { existing })
                    .SelectMany(x => GetModelsNames(new IniFile(x.ModelsFilename))).ToList();

            // Let’s find out if any KN5 files are missing, just in case
            string[] GetMissingKn5Files(IEnumerable<string> required) {
                return required?.Where(x => !existingModels.Any(y => string.Equals(x, y, StringComparison.OrdinalIgnoreCase))).ToArray()
                        ?? new string[0];
            }

            MissingKn5Files = GetMissingKn5Files(RequiredKn5Files);
            if (Layouts != null) {
                foreach (var layout in Layouts) {
                    layout.MissingKn5Files = GetMissingKn5Files(layout.RequiredKn5Files);
                }
            }

            var newModels = Layouts?.SelectMany(x => x.Kn5Files).ToList() ?? Kn5Files;

            // If there is a conflict, it will be changed later
            SharedModelsOverlap = newModels?.Any(existingModels.Contains) == true;

            if (Layouts == null) {
                if (!existing.MultiLayoutMode) {
                    // Simplest case — basic track+basic track, nothing complicated.
                    _existingFormat = $"Update for the track {existingName}";
                    NoLayoutsExistingLayout = existing;
                    NoConflictMode = false;
                } else {
                    // Multi-layout track installed, but user also has a basic track to install? OK…
                    var existingBasic = existing.LayoutId == null;
                    if (existingBasic) {
                        // Just an update, I guess?
                        _existingFormat = $"Update for the track {existingName}";
                        NoLayoutsExistingLayout = existing;
                        NoConflictMode = false;
                    } else {
                        // There is no basic track! So, it’s like an additional layout
                        _existingFormat = Name == existingName ?
                                $"New layout for the track {existingName}" :
                                $"New layout {Name} for the track {existingName}";
                        NoConflictMode = true;
                    }
                }
            } else {
                // Sometimes, basic track might end up in layouts if there are other layouts.
                var newBasicLayout = Layouts.FirstOrDefault(x => x.Id == "");
                var newBasic = newBasicLayout != null;

                if (!existing.MultiLayoutMode) {
                    if (!newBasic) {
                        // Simple case: basic track installed, additional layouts are being added
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"New {{layout}} for the track {existingName}");

                        NoConflictMode = true;
                    } else {
                        // Basic track installed, user is adding additional layouts, but one of them is basic as well! What to do?…
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"Update for the track {existingName}, plus additional {{layout}}");
                        newBasicLayout.ExistingLayout = existing;
                        HasNewExtraLayouts = true;
                        NoConflictMode = false;
                    }
                } else {
                    // Oops… Layouts+layouts.
                    // Is already installed track has that thing when one of layouts is basic track?
                    var existingBasic = existing.LayoutId == null;
                    var newLayouts = Layouts.Count(x => existing.GetLayoutByLayoutId(x.Id) == null);

                    if (!(existingBasic && newBasic) && newLayouts == Layouts.Count) {
                        // Blessed case! No conflicts
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"New {{layout}} for the track {existingName}");
                        NoConflictMode = true;
                    } else {
                        // What can I say…
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count, newLayouts > 0 ?
                                $"Update for the track {existingName}, plus additional {{layout}}" :
                                $"Update for the track {existingName}");
                        HasNewExtraLayouts = newLayouts > 0;
                        NoConflictMode = false;

                        foreach (var layout in Layouts) {
                            layout.ExistingLayout = existing.GetLayoutByLayoutId(layout.Id);
                        }

                        if (newBasic && existingBasic){
                            newBasicLayout.ExistingLayout = existing;
                        }
                    }
                }
            }

            // Good luck with testing, lol
            // x_x
        }

        private List<string> _overlappedModels;
        public string DisplayOverlappedModels => _overlappedModels?.JoinToReadableString();

        private void UpdateSharedModelsOverlap() {
            List<string> overlappedModels;

            var existing = GetExistingAcObject();
            if (existing == null || Layouts?.All(x => !x.Active) == true) {
                overlappedModels = null;
            } else {
                var activeLayouts = Layouts?.Where(x => x.Active).ToList();

                // Already installed models apart from models which are ready to be installed by specific layouts
                var existingModels = ((IEnumerable<TrackObjectBase>)existing.MultiLayouts ?? new TrackObjectBase[] { existing })
                        .SelectMany(x => GetModelsNames(new IniFile(x.ModelsFilename)).ApartFrom(
                                activeLayouts == null
                                        ? (x.LayoutId == null ? Kn5Files : null)
                                        : activeLayouts.GetByIdOrDefault(x.LayoutId ?? "", StringComparison.InvariantCulture)?.Kn5Files))
                        .Distinct().ToList();

                if (existingModels.Count == 0) {
                    overlappedModels = null;
                } else if (activeLayouts != null) {
                    overlappedModels = activeLayouts.SelectMany(x => x.Kn5Files).Where(existingModels.Contains).Distinct().ToList();
                } else if (NoLayoutsExistingLayout != null) {
                    // If current track as a layout is an update for the existing one, then there are no previous shared models
                    overlappedModels = null;
                } else {
                    overlappedModels = Kn5Files?.Where(existingModels.Contains).ToList();
                }
            }

            _overlappedModels = overlappedModels ?? new List<string>();
            OnPropertyChanged(nameof(DisplayOverlappedModels));
            SharedModelsOverlap = _overlappedModels.Count > 0;
        }

        private void OnLayoutPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(TrackContentLayoutEntry.Active)) {
                UpdateSharedModelsOverlap();
            }
        }

        protected override void OnSelectedOptionChanged(UpdateOption oldValue, UpdateOption newValue) {
            base.OnSelectedOptionChanged(oldValue, newValue);
            UpdateSharedModelsOverlap();
        }

        private bool _hasNewExtraLayouts;

        public bool HasNewExtraLayouts {
            get => _hasNewExtraLayouts;
            set {
                if (Equals(value, _hasNewExtraLayouts)) return;
                _hasNewExtraLayouts = value;
                OnPropertyChanged();
            }
        }

        private bool _sharedModelsOverlap;

        public bool SharedModelsOverlap {
            get => _sharedModelsOverlap;
            set {
                if (value == _sharedModelsOverlap) return;
                _sharedModelsOverlap = value;
                OnPropertyChanged();
            }
        }

        private bool _keepExistingSharedModels = true;

        public bool KeepExistingSharedModels {
            get => _keepExistingSharedModels;
            set {
                if (Equals(value, _keepExistingSharedModels)) return;
                _keepExistingSharedModels = value;
                OnPropertyChanged();
            }
        }

        public override string GenericModTypeName => "Track";
        public override string NewFormat => ToolsStrings.ContentInstallation_TrackNew;

        private string _existingFormat = ToolsStrings.ContentInstallation_TrackExisting;
        public override string ExistingFormat => _existingFormat;

        protected override ICopyCallback GetCopyCallback(string destination) {
            var filter = NoConflictMode ? null : SelectedOption?.Filter;

            Logging.Write("INSTALLING TRACK…");

            UpdateSharedModelsOverlap();
            if (SharedModelsOverlap && KeepExistingSharedModels) {
                Logging.Write($"We need to keep shared models: {_overlappedModels.JoinToString(", ")}");

                var shared = _overlappedModels;
                filter = filter.And(path => !shared.Any(x => FileUtils.ArePathsEqual(x, path)));
            }

            var disabled = Layouts?.Where(x => !x.Active).Select(x => x.Id).ToList();
            if (disabled?.Count > 0) {
                Logging.Write($"Disabled layouts: {disabled.JoinToString(", ")}");

                if (disabled.Count == Layouts.Count) {
                    Logging.Write("Everything is disabled!");
                    return new CopyCallback(null);
                }

                var inisToCopy = Layouts.Where(x => x.Active).Select(x => x.Id == "" ? "models.ini" : $@"models_{x.Id}.ini")
                                        .Select(FileUtils.NormalizePath).Distinct().ToList();
                var modelsToCopy = Layouts.Where(x => x.Active).SelectMany(x => x.Kn5Files)
                                          .Select(FileUtils.NormalizePath).Distinct().ToList();
                Logging.Write($"INIs to copy: {inisToCopy.JoinToString(", ")}");
                Logging.Write($"Models to copy: {modelsToCopy.JoinToString(", ")}");

                var mainDisabled = disabled.Contains("");
                if (mainDisabled) {
                    disabled.Remove("");
                }

                filter = filter.And(path => string.IsNullOrEmpty(Path.GetDirectoryName(path))
                        // If file is in track’s root directory
                        ? modelsToCopy.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)) ||
                                inisToCopy.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase))
                        // If file is in subfolder in track’s root directory
                        : FileUtils.Affects(@"ui", path)
                                ? (string.Equals(Path.GetDirectoryName(path), @"ui", StringComparison.OrdinalIgnoreCase) ? !mainDisabled :
                                        !disabled.Any(x => FileUtils.Affects($@"ui\{x}", path)))
                                : !disabled.Any(x => FileUtils.Affects(x, path)));
            }

            return new CopyCallback(fileInfo => {
                var filename = fileInfo.Key;
                if (EntryPath != string.Empty && !FileUtils.Affects(EntryPath, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, EntryPath);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            });
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            UpdateSharedModelsOverlap();

            if (NoConflictMode) {
                return new[] { new UpdateOption("Just install") };
            }

            bool UiFilter(string x) {
                if (!FileUtils.Affects("ui", x)) return true;

                var name = Path.GetFileName(x).ToLowerInvariant();
                return name != "ui_track.json" && name != "preview.png" && name != "outline.png";
            }

            return base.GetUpdateOptions().Concat(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter }
            }.NonNull());
        }

        public override FileAcManager<TrackObject> GetManager() {
            return TracksManager.Instance;
        }
    }

    public class ShowroomContentEntry : ContentEntryBase<ShowroomObject> {
        public override double Priority => 70d;

        public ShowroomContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string GenericModTypeName => "Showroom";
        public override string NewFormat => ToolsStrings.ContentInstallation_ShowroomNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_ShowroomExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui\ui_showroom.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview") { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview") { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }

        public override FileAcManager<ShowroomObject> GetManager() {
            return ShowroomsManager.Instance;
        }
    }

    public class CarSkinContentEntry : ContentEntryBase<CarSkinObject> {
        public override double Priority => 30d;

        [NotNull]
        private readonly CarObject _car;

        public CarSkinContentEntry([NotNull] string path, [NotNull] string id, [NotNull] string carId, string name = null, byte[] iconData = null)
                : base(path, id, name, null, iconData) {
            _car = CarsManager.Instance.GetById(carId) ?? throw new Exception($"Car “{carId}” for the skin not found");
            NewFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinNew, "{0}", _car.DisplayName);
            ExistingFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinExisting, "{0}", _car.DisplayName);
        }

        public override string GenericModTypeName => "Car skin";
        public override string NewFormat { get; }
        public override string ExistingFormat { get; }

        public override FileAcManager<CarSkinObject> GetManager() {
            return _car.SkinsManager;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui_skin.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview") { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview") { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }
    }

    public class TrackSkinContentEntry : ContentEntryBase<TrackSkinObject> {
        public override double Priority => 40d;

        [NotNull]
        private readonly TrackObject _track;

        public TrackSkinContentEntry([NotNull] string path, [NotNull] string id, [NotNull] string trackId, string name, string version, byte[] iconData = null)
                : base(path, id, name, version, iconData) {
            _track = TracksManager.Instance.GetById(trackId) ?? throw new Exception($"Track “{trackId}” for the skin not found");
            NewFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinNew, "{0}", _track.DisplayName);
            ExistingFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinExisting, "{0}", _track.DisplayName);
        }

        public override string GenericModTypeName => "Track skin";
        public override string NewFormat { get; }
        public override string ExistingFormat { get; }

        public override FileAcManager<TrackSkinObject> GetManager() {
            return _track.SkinsManager;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui_track_skin.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.png");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview") { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview") { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }
    }

    public class FontContentEntry : ContentEntryBase<FontObject> {
        public override double Priority => 20d;

        public FontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "Font";
        public override string NewFormat => ToolsStrings.ContentInstallation_FontNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_FontExisting;

        public override FileAcManager<FontObject> GetManager() {
            return FontsManager.Instance;
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var bitmapExtension = Path.GetExtension(EntryPath);
            var mainEntry = EntryPath.ApartFromLast(bitmapExtension) + FontObject.FontExtension;

            return new CopyCallback(info => {
                if (FileUtils.ArePathsEqual(info.Key, mainEntry)) {
                    return destination;
                }

                if (FileUtils.ArePathsEqual(info.Key, EntryPath)) {
                    return destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                }

                return null;
            });
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class TrueTypeFontContentEntry : ContentEntryBase<TrueTypeFontObject> {
        public override double Priority => 15d;

        public TrueTypeFontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "TrueType font";
        public override string NewFormat => "New TrueType font “{0}”";
        public override string ExistingFormat => "Update for the TrueType font “{0}”";

        public override FileAcManager<TrueTypeFontObject> GetManager() {
            return TrueTypeFontsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class PythonAppContentEntry : ContentEntryBase<PythonAppObject> {
        public override double Priority => 45d;

        [CanBeNull]
        private readonly List<string> _icons;

        public PythonAppContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null,
                byte[] iconData = null, IEnumerable<string> icons = null) : base(path, id, name, version, iconData) {
            MoveEmptyDirectories = true;
            _icons = icons?.ToList();
        }

        public override string GenericModTypeName => "App";
        public override string NewFormat => "New app “{0}”";
        public override string ExistingFormat => "Update for the app “{0}”";

        public override FileAcManager<PythonAppObject> GetManager() {
            return PythonAppsManager.Instance;
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var callback = base.GetCopyCallback(destination);
            var icons = _icons;
            if (icons == null) return callback;

            return new CopyCallback(info => {
                var b = callback?.File(info);
                return b != null || !icons.Contains(info.Key) ? b :
                        Path.Combine(AcPaths.GetGuiIconsFilename(AcRootDirectory.Instance.RequireValue),
                                Path.GetFileName(info.Key) ?? "icon.tmp");
            }, info => callback?.Directory(info));
        }
    }

    public class WeatherContentEntry : ContentEntryBase<WeatherObject> {
        public override double Priority => 25d;

        public WeatherContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "Weather";
        public override string NewFormat => ToolsStrings.ContentInstallation_WeatherNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_WeatherExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool PreviewFilter(string x) {
                return x != @"preview.jpg";
            }

            IEnumerable<string> RemoveClouds(string location) {
                yield return Path.Combine(location, "clouds");
            }

            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true },
                new UpdateOption("Update over existing files, remove existing clouds if any"){ CleanUp = RemoveClouds },
                new UpdateOption("Update over existing files, keep preview"){ Filter = PreviewFilter },
                new UpdateOption("Update over existing files, remove existing clouds if any & keep preview"){ Filter = PreviewFilter, CleanUp = RemoveClouds },
            };
        }

        protected override UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.ElementAtOrDefault(2) ?? base.GetDefaultUpdateOption(list);
        }

        public override FileAcManager<WeatherObject> GetManager() {
            return WeatherManager.Instance;
        }
    }

    public class PpFilterContentEntry : ContentEntryBase<PpFilterObject> {
        public override double Priority => 27d;

        public PpFilterContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "PP-Filter";
        public override string NewFormat => "New PP-filter “{0}”";
        public override string ExistingFormat => "Update for the PP-filter “{0}”";

        public override FileAcManager<PpFilterObject> GetManager() {
            return PpFiltersManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class DriverModelContentEntry : ContentEntryBase<DriverModelObject> {
        public override double Priority => 21d;

        public DriverModelContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "Driver model";
        public override string NewFormat => "New driver model “{0}”";
        public override string ExistingFormat => "Update for the driver model “{0}”";

        public override FileAcManager<DriverModelObject> GetManager() {
            return DriverModelsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }
}
