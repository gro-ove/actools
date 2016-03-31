using System;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class TracksManager : AcManagerNew<TrackObject> {
        public static TracksManager Instance { get; private set; }

        public static TracksManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new TracksManager();
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.TracksDirectories;

        public override TrackObject GetDefault() {
            return base.GetById("imola") ?? base.GetDefault();
        }

        public override TrackObject GetById(string id) {
            return base.GetById(id.Contains('/') ? id.Split('/')[0] : id);
        }

        public TrackBaseObject GetLayoutById(string id) {
            if (!id.Contains('/')) return base.GetById(id);
            return base.GetById(id.Split('/')[0])?.GetLayoutById(id);
        }

        protected override TrackObject CreateAcObject(string id, bool enabled) {
            return new TrackObject(this, id, enabled);
        }

        protected override bool ShouldSkipFileInternal(string filename) {
            return filename.EndsWith(".ai", StringComparison.OrdinalIgnoreCase);
        }
    }
}
