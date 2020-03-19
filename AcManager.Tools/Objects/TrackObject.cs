using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class TrackObject : TrackObjectBase {
        public sealed override string LayoutId { get; }

        [NotNull]
        public sealed override string IdWithLayout { get; }

        [CanBeNull]
        public BetterObservableCollection<TrackObjectBase> MultiLayouts { get; }

        [CanBeNull]
        private readonly string _layoutLocation;

        public bool MultiLayoutMode => MultiLayouts != null;

        public TrackObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            InitializeLocationsOnce();

            try {
                var information = GetLayouts();
                if (information != null) {
                    _layoutLocation = information.MainLayout;
                    InitializeLocationsInner(_layoutLocation);

                    LayoutId = information.SimpleMainLayout ? null : Path.GetFileName(_layoutLocation);
                    IdWithLayout = information.SimpleMainLayout ? Id : $@"{Id}/{LayoutId}";
                    MultiLayouts = new BetterObservableCollection<TrackObjectBase>(
                            information.AdditionalLayouts.Select(x => {
                                var c = new TrackExtraLayoutObject(manager, this, enabled, x);
                                c.PropertyChanged += Configuration_PropertyChanged;
                                c.LayoutPriority = GetLayoutData(x)?.GetDoubleValueOnly("priority") ?? 0;
                                return c;
                            }).OrderByDescending(x => x.LayoutPriority).ThenBy(x => x.IdWithLayout).Prepend((TrackObjectBase)this));

                    SkinsManager = InitializeSkins();
                    return;
                }
            } catch (AcErrorException e) {
                AddError(e.AcError);
            }

            InitializeLocationsInner(Path.Combine(Location, "ui"));
            _layoutLocation = null;
            LayoutId = null;
            IdWithLayout = Id;
            MultiLayouts = null;

            SkinsManager = InitializeSkins();
        }

        private TrackObjectBase _selectedLayout;

        [NotNull]
        public TrackObjectBase SelectedLayout {
            get {
                if (!MultiLayoutMode) return this;
                if (_selectedLayout == null) {
                    var layoutId = LimitedStorage.Get(LimitedSpace.SelectedLayout, Id);
                    _selectedLayout = layoutId == null ? this : (GetLayoutByLayoutId(layoutId) ?? this);
                }
                return _selectedLayout;
            }
            set {
                if (!MultiLayoutMode || Equals(value, _selectedLayout)) return;
                _selectedLayout = value;
                OnPropertyChanged();

                if (value == this) {
                    LimitedStorage.Remove(LimitedSpace.SelectedLayout, Id);
                } else {
                    LimitedStorage.Set(LimitedSpace.SelectedLayout, Id, value.LayoutId);
                }
            }
        }

        protected void InitializeLocationsInner(string uiDirectory) {
            JsonFilename = Path.Combine(uiDirectory, "ui_track.json");
            PreviewImage = Path.Combine(uiDirectory, "preview.png");
            OutlineImage = Path.Combine(uiDirectory, "outline.png");
            SkinsDirectory = Path.Combine(Location, "skins", "cm_skins");
            DefaultSkinDirectory = Path.Combine(Location, "skins", "default");
            SkinsCombinedFilename = Path.Combine(DefaultSkinDirectory, "cm_skins_active.json");
        }

        protected override DateTime GetCreationDateTime() {
            if (File.Exists(_layoutLocation != null ? Path.Combine(_layoutLocation, @"dlc_ui_track.json")
                    : Path.Combine(Location, @"ui", @"dlc_ui_track.json"))) {
                var fileInfo = new FileInfo(JsonFilename);
                if (fileInfo.Exists) {
                    return fileInfo.CreationTime;
                }
            }

            return base.GetCreationDateTime();
        }

        private class LayoutsInformation {
            public string MainLayout;
            public List<string> AdditionalLayouts;
            public bool SimpleMainLayout;

            public int TotalLayouts => AdditionalLayouts.Count + 1;
        }

        /// <summary>
        /// </summary>
        /// <exception cref="AcErrorException"></exception>
        /// <returns></returns>
        [CanBeNull]
        private LayoutsInformation GetLayouts() {
            var uiDirectory = Path.Combine(Location, "ui");
            if (!Directory.Exists(uiDirectory)) throw new AcErrorException(this, AcErrorType.Data_UiDirectoryIsMissing);

            var basic = Path.Combine(uiDirectory, "ui_track.json");
            var additional = Directory.GetDirectories(uiDirectory).Where(x => File.Exists(Path.Combine(x, "ui_track.json"))).ToList();
            if (additional.Count == 0) return null;

            if (File.Exists(basic)) {
                return new LayoutsInformation {
                    MainLayout = uiDirectory,
                    AdditionalLayouts = additional.ToList(),
                    SimpleMainLayout = true
                };
            }

            var main = additional.Count > 1 ? GetKunosMainLayout(Id, additional) : additional[0];
            additional.Remove(main);

            return new LayoutsInformation {
                MainLayout = main,
                AdditionalLayouts = additional,
                SimpleMainLayout = false
            };
        }

        [CanBeNull]
        private static JObject GetLayoutData(string uiDirectory) {
            var filename = Path.Combine(uiDirectory, "ui_track.json");

            bool loaded;
            JObject value;
            lock (RecentlyParsed) {
                loaded = RecentlyParsed.TryGetValue(filename, out value);
            }

            if (!loaded) {
                try {
                    value = JsonExtension.Parse(FileUtils.ReadAllText(filename));
                    lock (RecentlyParsed) {
                        RecentlyParsed[filename] = value;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                    return null;
                }
            }

            return value;
        }

        private static readonly Regex NonDefaultLayout = new Regex("downhill|drift|fall|freeroam|grid|mini|no ?chicane|osrw|oval|pursuit|rev(?:\b|erse)?",
                RegexOptions.Compiled);

        private static readonly Regex PreferredLayout = new Regex("circuit|international|full|gp|grand?|hill ?climb|normal|standar[dt]?|uphill",
                RegexOptions.Compiled);

        private static int GetWeight(Match m) {
            if (m.Value == "full" || m.Value == "drift" || m.Value == "normal" || m.Value == "pursuit") return 10;
            if (m.Value == "international" || m.Value == "circuit") return 2;
            return m.Length;
        }

        [CanBeNull]
        private static string GetPreferredMainLayout(string trackId, IEnumerable<string> uiDirectories) {
            string preferredLayout = null;
            var preferredLayoutPriority = double.MinValue;

            trackId = trackId.ToLowerInvariant();
            foreach (var layout in uiDirectories) {
                var layoutId = Path.GetFileName(layout)?.ToLowerInvariant();
                if (layoutId == null) continue;

                var data = GetLayoutData(layout);
                var name = (data?.GetStringValueOnly("name") ?? layoutId).ToLowerInvariant();
                var distance = name.ComputeLevenshteinDistance(trackId);

                double priority = (100 - distance * 10).Clamp(0, 100) - name.Length;
                priority -= NonDefaultLayout.Matches(layoutId).Cast<Match>().Sum(GetWeight) * 10;
                priority += PreferredLayout.Matches(layoutId).Cast<Match>().Sum(GetWeight) * 10;
                priority -= NonDefaultLayout.Matches(name).Cast<Match>().Sum(GetWeight) * 100;
                priority += PreferredLayout.Matches(name).Cast<Match>().Sum(GetWeight) * 100;
                priority += data?.GetDoubleValueOnly("priority") * 1e8 ?? 0;

                // b.Append($"\n{layoutId}: {name}, distance={distance}, priority={priority}");
                if (preferredLayoutPriority < priority) {
                    preferredLayoutPriority = priority;
                    preferredLayout = layout;
                }
            }

            return preferredLayout;
        }

        [NotNull]
        private static string GetKunosMainLayout(string trackId, List<string> uiDirectories) {
            var kunosTracks = DataProvider.Instance.GetKunosContentIds(@"tracks");
            if (kunosTracks != null && Array.IndexOf(kunosTracks, trackId) != -1) {
                var layouts = DataProvider.Instance.GetKunosContentIds(@"layouts");
                if (layouts != null) {
                    var kunosLayout = GetPreferredMainLayout(trackId,
                            uiDirectories.Where(x => Array.IndexOf(layouts, $@"{trackId}/{Path.GetFileName(x)}") != -1));
                    if (kunosLayout != null) {
                        return kunosLayout;
                    }
                }
            }

            return GetPreferredMainLayout(trackId, uiDirectories) ?? uiDirectories[0];
        }

        private bool IsMultiLayoutsChanged() {
            var previous = MultiLayouts != null;

            LayoutsInformation information;
            try {
                information = GetLayouts();
            } catch (Exception) {
                return previous;
            }

            if (information == null) {
                return MultiLayouts != null;
            }

            if (MultiLayouts == null) {
                return true;
            }

            return information.TotalLayouts != MultiLayouts.Count ||
                    !string.Equals(information.MainLayout, _layoutLocation, StringComparison.OrdinalIgnoreCase) ||
                    information.AdditionalLayouts.Any((x, i) => !string.Equals(x, MultiLayouts[i + 1].Location, StringComparison.OrdinalIgnoreCase));
        }

        public override void Reload() {
            if (IsMultiLayoutsChanged()) {
                Manager.Reload(Id);
                return;
            }

            base.Reload();
            SkinsManager.Rescan();

            if (MultiLayouts == null) return;
            foreach (var layout in MultiLayouts.Skip(1)) {
                layout.Reload();
            }

            if (MultiLayouts.Count > 1) {
                _commonName = FindNameForMultiLayoutMode(MultiLayouts);
            }
        }

        /// <summary>
        /// Get layout by id+layout id.
        /// </summary>
        /// <param name="idWithLayout">Id with layout id ("ks_nordschleife/touristenfahrten")</param>
        /// <returns></returns>
        [CanBeNull]
        public TrackObjectBase GetLayoutById(string idWithLayout) {
            return MultiLayouts?.FirstOrDefault(x => x.IdWithLayout.Equals(idWithLayout, StringComparison.OrdinalIgnoreCase)) ??
                    (idWithLayout == Id ? this : null);
        }

        /// <summary>
        /// Get layout by its id.
        /// </summary>
        /// <param name="layoutId">Layout id ("touristenfahrten", not "ks_nordschleife/touristenfahrten"!)</param>
        /// <returns></returns>
        [CanBeNull]
        public TrackObjectBase GetLayoutByLayoutId(string layoutId) {
            return MultiLayouts?.FirstOrDefault(x => x.LayoutId?.Equals(layoutId, StringComparison.OrdinalIgnoreCase) == true);
        }

        private bool _extraLayoutChanged;

        public bool ExtraLayoutChanged {
            get => _extraLayoutChanged;
            set {
                if (Equals(value, _extraLayoutChanged)) return;
                _extraLayoutChanged = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Changed));
            }
        }

        public override bool Changed {
            get => base.Changed || ExtraLayoutChanged;
            protected set => base.Changed = value;
        }

        private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(Changed)) return;
            ExtraLayoutChanged = MultiLayouts?.Skip(1).Any(x => x.Changed) == true;
        }

        protected override void LoadOrThrow() {
            _extraLayoutChanged = false;
            base.LoadOrThrow();

            if (MultiLayouts == null) return;
            foreach (var extraLayout in MultiLayouts.Skip(1)) {
                extraLayout.Load();
            }
            if (MultiLayouts.Count > 1) {
                _commonName = FindNameForMultiLayoutMode(MultiLayouts);
            }
        }

        [CanBeNull]
        public static string FindNameForMultiLayoutMode(IReadOnlyList<string> obj) {
            var baseName = obj[0];
            if (baseName == null) return null;

            for (var i = obj.Where(x => x != null).Select(x => x.Length).Min(); i > 2; i--) {
                var result = baseName.Substring(0, i);
                if (obj.Skip(1).Any(x => x?.Substring(0, i) != result)) continue;

                result = result.Trim();
                if (result.Length > 2 && (result.EndsWith(@"-") || result.EndsWith(@"—") || result.EndsWith(@"/") || result.EndsWith(@"("))) {
                    result = result.Substring(0, result.Length - 1).Trim();
                }
                return result;
            }

            return null;
        }

        [CanBeNull]
        private static string FindNameForMultiLayoutMode(IReadOnlyList<TrackObjectBase> obj) {
            return FindNameForMultiLayoutMode(obj.Select(x => x.Name).ToList());
        }

        public override bool HandleChangedFile(string filename) {
            if (IsMultiLayoutsChanged()) return false;

            if (MultiLayouts != null) {
                foreach (var layout in MultiLayouts.Skip(1).Where(layout => FileUtils.IsAffectedBy(filename, layout.Location))) {
                    return layout.HandleChangedFile(filename);
                }
            }

            base.HandleChangedFile(filename);
            return true;
        }

        public override async Task SaveAsync() {
            await base.SaveAsync();

            if (MultiLayouts == null) return;
            foreach (var layout in MultiLayouts.Skip(1)) {
                await layout.SaveAsync();
            }
        }

        [CanBeNull]
        private string _commonName;

        public override string NameEditable {
            get => (MultiLayoutMode ? _commonName : null) ?? base.NameEditable;
            set {
                if (MultiLayouts != null && _commonName != null) {
                    if (Equals(value, _commonName)) return;

                    base.NameEditable = value + Name?.Substring(_commonName.Length);
                    foreach (var layout in MultiLayouts.Skip(1)) {
                        layout.NameEditable = value + layout.Name?.Substring(_commonName.Length);
                    }

                    _commonName = value;
                } else {
                    base.NameEditable = value;
                }
            }
        }

        public override string LayoutName {
            get => base.NameEditable;
            set => base.NameEditable = value;
        }

        public override string DisplayName => MultiLayouts?.Count > 1 ?
                $@"{_commonName ?? base.DisplayName} ({MultiLayouts.Count})" : base.DisplayName;

        public string DisplayNameWithoutCount => MultiLayouts?.Count > 1 ? _commonName : base.DisplayName;

        public override TrackObject MainTrackObject => this;

        public override string LayoutDataDirectory => LayoutId == null ? Location : Path.Combine(Location, LayoutId);
    }
}