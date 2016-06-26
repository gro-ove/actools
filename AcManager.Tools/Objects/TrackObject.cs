using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class TrackObject : TrackBaseObject {
        [CanBeNull]
        public sealed override string LayoutId { get; }

        [NotNull]
        public sealed override string IdWithLayout { get; }

        [CanBeNull]
        public BetterObservableCollection<TrackBaseObject> MultiLayouts { get; }

        [CanBeNull]
        private readonly string _layoutLocation;

        public bool MultiLayoutMode => MultiLayouts != null;

        public TrackObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            InitializeLocationsOnce();
            try {
                var list = GetMultiLayouts();
                if (IsInMultiLayoutsMode(list)) {
                    _layoutLocation = list[0];
                    InitializeLocationsInner(_layoutLocation);

                    LayoutId = Path.GetFileName(_layoutLocation);
                    IdWithLayout = $"{Id}/{LayoutId}";
                    MultiLayouts = new BetterObservableCollection<TrackBaseObject>(
                            list.Skip(1).Select(x => {
                                var c = new TrackExtraLayoutObject(manager, id, enabled, x);
                                c.PropertyChanged += Configuration_PropertyChanged;
                                return c;
                            }).Prepend((TrackBaseObject)this));
                    return;
                }
            } catch (AcErrorException e) {
                AddError(e.AcError);
                IdWithLayout = Id;
            }

            InitializeLocationsInner(Path.Combine(Location, "ui"));
            IdWithLayout = Id;
        }

        protected void InitializeLocationsInner(string uiDirectory) {
            JsonFilename = Path.Combine(uiDirectory, "ui_track.json");
            PreviewImage = Path.Combine(uiDirectory, "preview.png");
            OutlineImage = Path.Combine(uiDirectory, "outline.png");
        }

        /// <summary>
        /// </summary>
        /// <exception cref="AcErrorException"></exception>
        /// <returns></returns>
        private List<string> GetMultiLayouts() {
            var uiDirectory = Path.Combine(Location, "ui");
            if (!Directory.Exists(uiDirectory)) throw new AcErrorException(this, AcErrorType.Data_UiDirectoryIsMissing);
            return Directory.GetDirectories(uiDirectory).Where(x => File.Exists(Path.Combine(x, "ui_track.json"))).ToList();
        }

        private bool IsInMultiLayoutsMode(IEnumerable<string> list) => !File.Exists(Path.Combine(Location, "ui", "ui_track.json")) && list.Any();

        private bool IsMultiLayoutsChanged() {
            var previous = MultiLayouts != null;

            List<string> list;
            try {
                list = GetMultiLayouts();
            } catch (Exception) {
                return previous;
            }

            var actual = IsInMultiLayoutsMode(list);
            if (MultiLayouts == null) {
                return actual;
            }

            return previous != actual || list.Count != MultiLayouts.Count ||
                   list[0] != _layoutLocation ||
                   list.Skip(1).Any((x, i) => !x.Equals(MultiLayouts[i + 1].Location, StringComparison.OrdinalIgnoreCase));
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
        public TrackBaseObject GetLayoutById(string idWithLayout) {
            return MultiLayouts?.FirstOrDefault(x => x.IdWithLayout.Equals(idWithLayout, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get layout by its id.
        /// </summary>
        /// <param name="layoutId">Layout id ("touristenfahrten", not "ks_nordschleife/touristenfahrten"!)</param>
        /// <returns></returns>
        public TrackBaseObject GetLayoutByLayoutId(string layoutId) {
            return MultiLayouts?.FirstOrDefault(x => x.LayoutId.Equals(layoutId, StringComparison.OrdinalIgnoreCase));
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

        private static string FindNameForMultiLayoutMode(IReadOnlyList<TrackBaseObject> obj) {
            var baseName = obj[0].Name;
            if (baseName == null) return null;

            for (var i = obj.Where(x => x.Name != null).Select(x => x.Name.Length).Min(); i > 2; i--) {
                var result = baseName.Substring(0, i);
                if (obj.Skip(1).Any(x => x.Name?.Substring(0, i) != result)) continue;

                result = result.Trim();
                if (result.Length > 2 && result.EndsWith("-") || result.EndsWith("—")) {
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
                _commonName + " (" + MultiLayouts.Count + ")" : base.DisplayName;

        public override TrackObject MainTrackObject => this;
    }
}
