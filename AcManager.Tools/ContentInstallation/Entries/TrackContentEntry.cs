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
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
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
            set => Apply(value, ref _noLayoutsExistingLayout);
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
            set => Apply(value, ref _hasNewExtraLayouts);
        }

        private bool _sharedModelsOverlap;

        public bool SharedModelsOverlap {
            get => _sharedModelsOverlap;
            set => Apply(value, ref _sharedModelsOverlap);
        }

        private bool _keepExistingSharedModels = true;

        public bool KeepExistingSharedModels {
            get => _keepExistingSharedModels;
            set => Apply(value, ref _keepExistingSharedModels);
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
                        : FileUtils.IsAffectedBy(path, @"ui")
                                ? (string.Equals(Path.GetDirectoryName(path), @"ui", StringComparison.OrdinalIgnoreCase) ? !mainDisabled :
                                        !disabled.Any(x => FileUtils.IsAffectedBy(path, $@"ui\{x}")))
                                : !disabled.Any(x => FileUtils.IsAffectedBy(path, x)));
            }

            return new CopyCallback(fileInfo => {
                var filename = fileInfo.Key;
                if (EntryPath != string.Empty && !FileUtils.IsAffectedBy(filename, EntryPath)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, EntryPath);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            });
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            UpdateSharedModelsOverlap();

            if (NoConflictMode) {
                return new[] { new UpdateOption("Just install", false) };
            }

            bool UiFilter(string x) {
                if (!FileUtils.IsAffectedBy(x, "ui")) return true;

                var name = Path.GetFileName(x).ToLowerInvariant();
                return name != "ui_track.json" && name != "preview.png" && name != "outline.png";
            }

            return base.GetUpdateOptions().Concat(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation, false) { Filter = UiFilter }
            }.NonNull());
        }

        public override FileAcManager<TrackObject> GetManager() {
            return TracksManager.Instance;
        }
    }
}