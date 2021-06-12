using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class TrackObject : IAcManagerScanWrapper {
        #region Initialization stuff
        public string SkinsDirectory { get; private set; }
        public string DefaultSkinDirectory { get; private set; }
        public string SkinsCombinedFilename { get; private set; }

        private List<string> _skinsActiveIds;

        private TrackSkinsManager InitializeSkins() {
            var manager = new TrackSkinsManager(Id, new InheritingAcDirectories(FileAcManager.Directories, SkinsDirectory), OnSkinsCollectionReady) {
                ScanWrapper = this
            };
            manager.Created += OnTrackSkinCreated;
            return manager;
        }

        /*private readonly CompositeObservableCollection<IAcError> _errors = new CompositeObservableCollection<IAcError>();
        public override ObservableCollection<IAcError> Errors => _errors;*/

        private void OnSkinsCollectionReady(object sender, EventArgs e) {
            var any = SkinsManager.GetDefault();
            if (any == null) {
                SelectedSkin = null;
            } else if (SelectedSkin == null) {
                SelectedSkin = any;
            }

            UpdateDisplayActiveSkins();
        }

        private void OnTrackSkinCreated(object sender, AcObjectEventArgs<TrackSkinObject> args) {
            if (_skinsActiveIds == null) {
                try {
                    _skinsActiveIds = File.Exists(SkinsCombinedFilename)
                            ? JArray.Parse(File.ReadAllText(SkinsCombinedFilename)).Select(x => (string)x).NonNull().ToList() : new List<string>();
                } catch (Exception e) {
                    _skinsActiveIds = new List<string>();
                    Logging.Warning(e);
                }
            }

            if (_skinsActiveIds.Contains(args.AcObject.Id)) {
                Logging.Debug("A");
                _busyApplyingSkins.Do(() => args.AcObject.IsActive = true);
                Logging.Debug("B");
            }
        }

        protected override void OnAcObjectOutdated() {
            foreach (var obj in SkinsManager.Loaded) {
                obj.Outdate();
            }

            base.OnAcObjectOutdated();
        }
        #endregion

        #region Rebuilding default skin
        private readonly Busy _busyApplyingSkins = new Busy();

        private bool _applyingSkins;

        public bool ApplyingSkins {
            get => _applyingSkins;
            private set => Apply(value, ref _applyingSkins);
        }

        private bool _again, _working;

        public void ForceSkinEnabled([NotNull] TrackSkinObject skin) {
            Logging.Debug("AA");
            if (_busyApplyingSkins.Is) {
                foreach (var other in EnabledOnlySkins.ApartFrom(skin)) {
                    other.IsActive = false;
                }
                skin.IsActive = true;
            } else {
                _busyApplyingSkins.Do(() => {
                    foreach (var other in EnabledOnlySkins.ApartFrom(skin)) {
                        other.IsActive = false;
                    }
                    skin.IsActive = true;
                    ApplySkins(GetActiveSkins());
                });
            }
            Logging.Debug("AB");
        }

        private void Try(Action action, ref int totalFailures, int attempts = 3) {
            for (var i = 0; i < attempts; i++) {
                try {
                    action();
                    break;
                } catch (Exception e) {
                    Logging.Warning(e.Message);
                    if (i == attempts - 1 || ++totalFailures > 7) throw;
                    Logging.Debug("Try again in 100 ms…");
                }

                Thread.Sleep(100);
            }
        }

        private void ApplySkins(List<EnabledSkinEntry> enabledSkins) {
            Logging.Debug("Applying track skins: " + enabledSkins.Select(x => x.Id).JoinToString("+"));

            var toInstallTemporary = new Dictionary<string, Tuple<double, string>>();

            void ScanDirectory(string subdirectory, double priority) {
                foreach (var file in Directory.GetFiles(subdirectory).Where(ShouldBeCopied)) {
                    var key = Path.GetFileName(file)?.ToLowerInvariant();
                    if (key == null || toInstallTemporary.TryGetValue(key, out var result) && result.Item1 > priority) continue;
                    toInstallTemporary[key] = Tuple.Create(priority, file);
                }
            }

            foreach (var skin in enabledSkins) {
                ScanDirectory(skin.Location, skin.Priority);

                foreach (var subdirectory in Directory.GetDirectories(skin.Location).NonNull()) {
                    var name = Path.GetFileName(subdirectory);
                    var ids = Regex.Split(name, @"[^\w-]").SkipWhile(x => x.Length == 0).ToList();
                    if (ids.All(x => enabledSkins.Any(y => string.Equals(y.Id, x, StringComparison.OrdinalIgnoreCase)))) {
                        ScanDirectory(subdirectory, skin.Priority + 100d * ids.Count);
                    }
                }
            }

            var directory = DefaultSkinDirectory;
            FileUtils.EnsureDirectoryExists(directory);

            var toInstall = toInstallTemporary.ToDictionary(x => x.Value.Item2, x => Path.Combine(directory, x.Key));
            // Logging.Debug(toInstall.Select(x => $"{x.Key.ApartFromFirst(Location)} → {x.Value.ApartFromFirst(Location)}").JoinToString('\n'));

            var skinsCombinedFileName = Path.GetFileName(SkinsCombinedFilename);
            var filenames = Directory.GetFiles(directory).Where(x => !string.Equals(Path.GetFileName(x), skinsCombinedFileName,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            var totalFailures = 0;

            if (filenames.Length == 0) {
                foreach (var p in toInstall) {
                    Try(() => FileUtils.HardLinkOrCopy(p.Key, p.Value), ref totalFailures);
                }
            } else {
                var mountPoint = FileUtils.GetMountPoint(directory);
                var files = filenames.Select(x => new {
                    Filename = x,
                    HardLinks = FileUtils.GetFileSiblingHardLinks(x, mountPoint)
                }).ToList();

                // Cleaning up overlapping files
                var recycle = new List<string>();
                foreach (var p in toInstall) {
                    var existing = files.FirstOrDefault(x => FileUtils.ArePathsEqual(x.Filename, p.Value));
                    if (existing != null) {
                        if (existing.HardLinks.Any(x => FileUtils.ArePathsEqual(x, p.Key))) {
                            // Already correct version, do not touch
                        } else if (existing.HardLinks.Any(x => !FileUtils.ArePathsEqual(Path.GetDirectoryName(x) ?? string.Empty, directory))) {
                            Try(() => File.Delete(existing.Filename), ref totalFailures);
                        } else {
                            recycle.Add(existing.Filename);
                        }
                    }
                }

                foreach (var unnecessary in files.Where(x => toInstall.Keys.All(y => !FileUtils.ArePathsEqual(x.Filename, y)))) {
                    // if (unnecessary.HardLinks.Any(x => !FileUtils.ArePathsEqual(Path.GetDirectoryName(x), directory))) {
                    if (unnecessary.HardLinks.Length > 1) {
                        Try(() => File.Delete(unnecessary.Filename), ref totalFailures);
                    } else {
                        recycle.Add(unnecessary.Filename);
                    }
                }

                FileUtils.Recycle(recycle.ToArray());

                // Copying new files
                foreach (var p in toInstall.Where(x => !File.Exists(x.Value))) {
                    Try(() => FileUtils.HardLinkOrCopy(p.Key, p.Value, true), ref totalFailures);
                }
            }

            // Done
            _skinsActiveIds = enabledSkins.Select(x => x.Id).ToList();
            if (_skinsActiveIds.Count != 0) {
                Try(() => File.WriteAllText(SkinsCombinedFilename, new JArray { _skinsActiveIds }.ToString(Formatting.Indented)), ref totalFailures);
            } else if (File.Exists(SkinsCombinedFilename)) {
                Try(() => File.Delete(SkinsCombinedFilename), ref totalFailures);
            }
        }

        private class EnabledSkinEntry {
            public string Id { get; }
            public string Location { get; }
            public double Priority { get; }

            public EnabledSkinEntry(string id, string location, double priority) {
                Id = id;
                Location = location;
                Priority = priority;
            }
        }

        private List<EnabledSkinEntry> GetActiveSkins() {
            return SkinsManager.Enabled.Where(x => x.IsActive)
                               .Select(x => new EnabledSkinEntry(x.Id, x.Location, x.Priority)).ToList();
        }

        internal void RefreshSkins(TrackSkinObject causeToRefresh) {
            if (_working) {
                _again = true;
                return;
            }

            UpdateDisplayActiveSkins();
            _busyApplyingSkins.Task(async () => {
                ApplyingSkins = true;

                if (causeToRefresh.IsActive) {
                    foreach (var skinObject in SkinsManager.Enabled.ApartFrom(causeToRefresh)) {
                        if (skinObject.Categories.Any(x => causeToRefresh.Categories.ContainsIgnoringCase(x))) {
                            skinObject.IsActive = false;
                        }
                    }
                }

                await Task.Delay(200);

                try {
                    _working = true;
                    do {
                        _again = false;
                        var enabled = GetActiveSkins();
                        await Task.Run(() => ApplySkins(enabled));
                    } while (_again);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t apply track skins", e);
                }

                _working = false;
                ApplyingSkins = false;
            });
        }

        private static bool ShouldBeCopied(string filename) {
            switch (Path.GetFileName(filename)?.ToLowerInvariant()) {
                case "preview.png":
                case null:
                    return false;
            }

            var ext = Path.GetExtension(filename)?.ToLowerInvariant();
            switch (ext) {
                case ".dds":
                case ".gif":
                case ".jpeg":
                case ".jpg":
                case ".png":
                case ".tga":
                case ".tif":
                case ".tiff":
                    return true;
            }

            return false;
        }
        #endregion

        /* for UI track’s skins manager */

        [NotNull]
        public TrackSkinsManager SkinsManager { get; }

        [NotNull]
        public AcEnabledOnlyCollection<TrackSkinObject> EnabledOnlySkins => SkinsManager.Enabled;

        private void UpdateDisplayActiveSkins() {
            if (EnabledOnlySkins.Count == 0) {
                DisplayActiveSkins = "No skins found";
                return;
            }

            var v = EnabledOnlySkins.Where(x => x.IsActive).Select(x => x.DisplayName).JoinToReadableString();
            DisplayActiveSkins = string.IsNullOrWhiteSpace(v) ? "No active skins" : v;
        }

        private string _displayActiveSkins;

        public string DisplayActiveSkins {
            get => _displayActiveSkins;
            set {
                if (Equals(value, _displayActiveSkins)) return;
                Logging.Debug(value);
                _displayActiveSkins = value;
                OnPropertyChanged();
            }
        }

        /* TODO: Force sorting by ID! */

        [CanBeNull]
        private TrackSkinObject _selectedSkin;

        [CanBeNull]
        public TrackSkinObject SelectedSkin {
            get {
                if (!SkinsManager.IsScanned) {
                    SkinsManager.Scan();
                }
                return _selectedSkin;
            }
            set {
                if (Equals(value, _selectedSkin)) return;
                _selectedSkin = value;
                OnPropertyChanged(nameof(SelectedSkin));

                if (_selectedSkin == null) return;
                if (_selectedSkin.Id == SkinsManager.WrappersList.FirstOrDefault()?.Value.Id) {
                    LimitedStorage.Remove(LimitedSpace.SelectedSkin, Id);
                } else {
                    LimitedStorage.Set(LimitedSpace.SelectedSkin, Id, _selectedSkin.Id);
                }
            }
        }

        private void SelectPreviousOrDefaultSkin() {
            var selectedSkinId = LimitedStorage.Get(LimitedSpace.SelectedSkin, Id);
            SelectedSkin = (selectedSkinId == null ? null : SkinsManager.GetById(selectedSkinId)) ?? SkinsManager.GetDefault();
        }

        void IAcManagerScanWrapper.AcManagerScan() {
            // ClearErrors(AcErrorCategory.TrackSkins);

            try {
                SkinsManager.ActualScan();
                // RemoveError(AcErrorType.TrackSkins_DirectoryIsUnavailable);
            } catch (IOException e) {
                // AddError(AcErrorType.TrackSkins_DirectoryIsUnavailable, e);
                Logging.Write("Track skins unhandled exception: " + e);
                return;
            }

            SelectPreviousOrDefaultSkin();
        }

        [CanBeNull]
        public TrackSkinObject GetSkinById([NotNull] string skinId) {
            return SkinsManager.GetById(skinId);
        }

        [CanBeNull]
        public TrackSkinObject GetSkinByIdFromConfig([NotNull] string skinId) {
            return string.IsNullOrWhiteSpace(skinId) || skinId == @"-" ? GetFirstSkinOrNull() : GetSkinById(skinId);
        }

        [CanBeNull]
        public TrackSkinObject GetFirstSkinOrNull() {
            return SkinsManager.GetFirstOrNull();
        }

        private AcWrapperCollectionView _skinsEnabledWrappersListView;

        public AcWrapperCollectionView SkinsEnabledWrappersList {
            get {
                if (_skinsEnabledWrappersListView != null) return _skinsEnabledWrappersListView;

                _skinsEnabledWrappersListView = new AcWrapperCollectionView(SkinsManager.WrappersAsIList) {
                    Filter = o => (o as AcItemWrapper)?.Value.Enabled == true
                };
                _skinsEnabledWrappersListView.MoveCurrentTo(SelectedSkin);
                _skinsEnabledWrappersListView.CurrentChanged +=
                        (sender, args) => { SelectedSkin = (_skinsEnabledWrappersListView.CurrentItem as AcItemWrapper)?.Loaded() as TrackSkinObject; };
                return _skinsEnabledWrappersListView;
            }
        }

        private BetterListCollectionView _skinsActualListView;

        public BetterListCollectionView SkinsActualList {
            get {
                if (_skinsActualListView != null) return _skinsActualListView;

                _skinsActualListView = new BetterListCollectionView(SkinsManager.Enabled);
                _skinsActualListView.MoveCurrentTo(SelectedSkin);
                _skinsActualListView.CurrentChanged += (sender, args) => { SelectedSkin = _skinsActualListView.CurrentItem as TrackSkinObject; };
                return _skinsActualListView;
            }
        }
    }
}