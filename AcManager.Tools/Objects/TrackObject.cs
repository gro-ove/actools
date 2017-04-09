using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class TrackObject : TrackObjectBase {
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
                                return c;
                            }).Prepend((TrackObjectBase)this));
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
        }

        private TrackObjectBase _selectedLayout;

        [NotNull]
        public TrackObjectBase SelectedLayout {
            get {
                if (!MultiLayoutMode) return this;
                if (_selectedLayout == null) {
                    var layoutId = LimitedStorage.Get(LimitedSpace.SelectedSkin, Id);
                    _selectedLayout = layoutId == null ? this : (GetLayoutByLayoutId(layoutId) ?? this);
                }
                return _selectedLayout;
            }
            set {
                if (!MultiLayoutMode || Equals(value, _selectedLayout)) return;
                _selectedLayout = value;
                OnPropertyChanged();

                if (value == this) {
                    LimitedStorage.Remove(LimitedSpace.SelectedSkin, Id);
                } else {
                    LimitedStorage.Set(LimitedSpace.SelectedSkin, Id, value.LayoutId);
                }
            }
        }

        protected void InitializeLocationsInner(string uiDirectory) {
            JsonFilename = Path.Combine(uiDirectory, "ui_track.json");
            PreviewImage = Path.Combine(uiDirectory, "preview.png");
            OutlineImage = Path.Combine(uiDirectory, "outline.png");
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

            return new LayoutsInformation {
                MainLayout = additional[0],
                AdditionalLayouts = additional.Skip(1).ToList(),
                SimpleMainLayout = false
            };
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
            get { return _extraLayoutChanged; }
            set {
                if (Equals(value, _extraLayoutChanged)) return;
                _extraLayoutChanged = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Changed));
            }
        }

        public override bool Changed {
            get { return base.Changed || ExtraLayoutChanged; }
            protected set { base.Changed = value; }
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
        private static string FindNameForMultiLayoutMode(IReadOnlyList<TrackObjectBase> obj) {
            var baseName = obj[0].Name;
            if (baseName == null) return null;

            for (var i = obj.Where(x => x.Name != null).Select(x => x.Name.Length).Min(); i > 2; i--) {
                var result = baseName.Substring(0, i);
                if (obj.Skip(1).Any(x => x.Name?.Substring(0, i) != result)) continue;

                result = result.Trim();
                if (result.Length > 2 && result.EndsWith(@"-") || result.EndsWith(@"—")) {
                    result = result.Substring(0, result.Length - 1).Trim();
                }
                return result;
            }

            return null;
        }

        public override bool HandleChangedFile(string filename) {
            if (IsMultiLayoutsChanged()) return false;

            if (MultiLayouts != null) {
                foreach (var layout in MultiLayouts.Skip(1).Where(layout => FileUtils.IsAffected(layout.Location, filename))) {
                    return layout.HandleChangedFile(filename);
                }
            }

            base.HandleChangedFile(filename);
            return true;
        }

        public override void Save() {
            base.Save();

            if (MultiLayouts == null) return;
            foreach (var layout in MultiLayouts.Skip(1)) {
                layout.Save();
            }
        }

        [CanBeNull]
        private string _commonName;

        public override string NameEditable {
            get { return (MultiLayoutMode ? _commonName : null) ?? base.NameEditable; }
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
            get { return base.NameEditable; }
            set { base.NameEditable = value; }
        }

        public override string DisplayName => MultiLayouts?.Count > 1 ?
                $@"{_commonName ?? base.DisplayName} ({MultiLayouts.Count})" : base.DisplayName;

        public override TrackObject MainTrackObject => this;

        public override string LayoutDataDirectory => LayoutId == null ? Location : Path.Combine(Location, LayoutId);
    }
}
