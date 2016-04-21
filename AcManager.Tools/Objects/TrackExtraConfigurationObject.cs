using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.Objects {
    public class TrackExtraLayoutObject : TrackBaseObject {
        public sealed override string Location { get; }

        public sealed override string LayoutId { get; }

        public TrackExtraLayoutObject(IFileAcManager manager, string id, bool enabled, string fixedLocation)
                : base(manager, id, enabled) {
            Location = fixedLocation;
            LayoutId = Path.GetFileName(fixedLocation);
            IdWithLayout = $"{Id}/{LayoutId}";
        }

        private TrackObject _mainTrackObject;

        public override TrackObject MainTrackObject {
            get {
                if (Id == null) return null;
                return _mainTrackObject ?? (_mainTrackObject = TracksManager.Instance.GetById(Id));
            }
        }

        public override string Name {
            get { return base.Name; }
            protected set {
                if (Equals(value, base.Name)) return;
                base.Name = value;
                OnPropertyChanged(nameof(LayoutName));
            }
        }

        public override string LayoutName {
            get { return NameEditable; }
            set { NameEditable = value; }
        }

        public sealed override string IdWithLayout { get; }

        public override string JsonFilename => Path.Combine(Location, "ui_track.json");

        public override string PreviewImage => ImageRefreshing ?? ImageRefreshing ?? Path.Combine(Location, "preview.png");

        public override string OutlineImage => ImageRefreshing ?? ImageRefreshing ?? Path.Combine(Location, "outline.png");
    }
}
