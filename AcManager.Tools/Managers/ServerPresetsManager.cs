using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class ServerPresetsManager : AcManagerNew<ServerPresetObject> {
        private static ServerPresetsManager _instance;

        public static ServerPresetsManager Instance => _instance ?? (_instance = new ServerPresetsManager());

        private static readonly string[] WatchedFiles = {
            @"server_cfg.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        private static readonly string Directory;

        static ServerPresetsManager() {
            Directory = Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"presets");
            System.IO.Directory.CreateDirectory(Directory);
        }

        public override IAcDirectories Directories { get; } = new AcDirectories(Directory);

        protected override ServerPresetObject CreateAcObject(string id, bool enabled) {
            return new ServerPresetObject(this, id, enabled);
        }
    }
}
