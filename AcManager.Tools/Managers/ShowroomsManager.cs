using System;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class ShowroomsManager : AcManagerNew<ShowroomObject> {
        private static ShowroomsManager _instance;

        public static ShowroomsManager Instance => _instance ?? (_instance = new ShowroomsManager());

        protected override ShowroomObject CreateAcObject(string id, bool enabled) {
            return new ShowroomObject(this, id, enabled);
        }

        private ShowroomsManager() {
            CupClient.Register(this, CupContentType.Showroom);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.ShowroomsDirectories;

        public override ShowroomObject GetDefault() {
            return GetById("showroom") ?? base.GetDefault();
        }

        private static readonly string[] WatchedFiles = {
            @"ui",
            @"ui\ui_showroom.json",
            @"preview.jpg",
            @"track.wav",
            // @"settings.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            if (WatchedFiles.ArrayContains(inner.ToLowerInvariant())) return false;
            return !filename.EndsWith(".kn5", StringComparison.OrdinalIgnoreCase) &&
                   !filename.EndsWith(".bank", StringComparison.OrdinalIgnoreCase);
        }
    }
}
