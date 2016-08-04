using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class SpecialEventsManager : AcManagerNew<SpecialEventObject> {
        private static SpecialEventsManager _instance;

        public static SpecialEventsManager Instance => _instance ?? (_instance = new SpecialEventsManager());

        private static readonly string[] WatchedFiles = {
            @"preview.png",
            @"event.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        public override IAcDirectories Directories { get; } = new AcDirectories(Path.Combine(AcRootDirectory.Instance.RequireValue, @"content", @"specialevents"), null);

        protected override SpecialEventObject CreateAcObject(string id, bool enabled) {
            return new SpecialEventObject(this, id, enabled);
        }
    }
}