using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public class TrackObject : TrackBaseObject {
        public sealed override string Location => base.Location;

        public sealed override string LayoutId { get; }

        public sealed override string IdWithLayout { get; }

        public BetterObservableCollection<TrackBaseObject> MultiLayouts { get; }

        public bool MultiLayoutMode => _layoutLocation != null;

        private readonly string _layoutLocation;

        public TrackObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            var uiDirectory = Path.Combine(Location, "ui");
            if (!Directory.Exists(uiDirectory)) {
                AddError(AcErrorType.Data_UiDirectoryIsMissing);
                _layoutLocation = null;
                return;
            }

            var multiLayouts = Directory.GetDirectories(uiDirectory).Where(x => File.Exists(Path.Combine(x, "ui_track.json"))).ToList();
            if (!multiLayouts.Any() || File.Exists(JsonFilename)) {
                IdWithLayout = Id;
                return;
            }

            _layoutLocation = multiLayouts[0];
            LayoutId = Path.GetFileName(_layoutLocation);
            IdWithLayout = $"{Id}/{LayoutId}";
            MultiLayouts = new BetterObservableCollection<TrackBaseObject>(
                    new[] { (TrackBaseObject)this }.Union(
                            multiLayouts.Skip(1).Select(x => {
                                var c = new TrackExtraLayoutObject(manager, id, enabled, x);
                                c.PropertyChanged += Configuration_PropertyChanged;
                                return c;
                            })));
        }

        public override void Reload() {
            base.Reload();
            // TODO: check if MultiLayoutMode is changed!
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

        private void Configuration_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "Changed" && ((TrackExtraLayoutObject)sender).Changed) {
                Changed = true;
            }
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            if (!MultiLayoutMode) return;

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
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            var local = filename.SubstringExt(Location.Length + 1).ToLower();
            if (local.StartsWith(@"ui\")) {
                return false;
            }

            return true;
        }

        public sealed override string JsonFilename => Path.Combine(_layoutLocation ?? Path.Combine(Location, "ui"), "ui_track.json");

        public override string PreviewImage => ImageRefreshing ?? Path.Combine(_layoutLocation ?? Path.Combine(Location, "ui"), "preview.png");

        public override string OutlineImage => ImageRefreshing ?? Path.Combine(_layoutLocation ?? Path.Combine(Location, "ui"), "outline.png");

        public override void Save() {
            base.Save();

            if (!MultiLayoutMode) return;
            foreach (var layout in MultiLayouts.Skip(1)) {
                layout.Save();
            }
        }

        private string _commonName;

        public override string NameEditable {
            get { return (MultiLayoutMode ? _commonName : null) ?? base.NameEditable; }
            set {
                if (MultiLayoutMode && _commonName != null) {
                    if (Equals(value, _commonName)) return;

                    base.NameEditable = value + Name.Substring(_commonName.Length);
                    foreach (var layout in MultiLayouts.Skip(1)) {
                        layout.NameEditable = value + layout.Name.Substring(_commonName.Length);
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

        public override string DisplayName => MultiLayoutMode && MultiLayouts.Count > 1 ?
                _commonName + " (" + MultiLayouts.Count + ")" : base.DisplayName;

        public override TrackObject MainTrackObject => this;
    }
}
