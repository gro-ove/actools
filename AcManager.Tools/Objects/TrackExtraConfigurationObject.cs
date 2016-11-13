using System.IO;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class TrackExtraLayoutObject : TrackObjectBase {
        private readonly string _location;

        [NotNull]
        public sealed override string LayoutId { get; }

        public TrackExtraLayoutObject(IFileAcManager manager, [NotNull] TrackObject parent, bool enabled, string fixedLocation)
                : base(manager, parent.Id, enabled) {
            _location = fixedLocation;
            MainTrackObject = parent;
            LayoutId = Path.GetFileName(fixedLocation) ?? "";
            IdWithLayout = $"{Id}/{LayoutId}";
        }

        protected override string GetLocation() {
            return _location;
        }

        public override TrackObject MainTrackObject { get; }

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

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, "ui_track.json");
            PreviewImage = Path.Combine(Location, "preview.png");
            OutlineImage = Path.Combine(Location, "outline.png");
        }

        public override ICommand ToggleCommand => MainTrackObject.ToggleCommand;

        public override ICommand DeleteCommand => MainTrackObject.DeleteCommand;

        public override ICommand SaveCommand => MainTrackObject.SaveCommand;

        public override ICommand ViewInExplorerCommand => MainTrackObject.ViewInExplorerCommand;

        public override string MapDirectory => Path.Combine(MainTrackObject.Location, LayoutId);
    }
}
