using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.AcManagersNew {
    public class WeatherManagerNew : AcManagerNew<WeatherObjectNew> {
        public static WeatherManagerNew Instance { get; private set; }

        public static WeatherManagerNew Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new WeatherManagerNew();
        }

        protected override bool ShouldSkipFileInternal(string filename) {
            return !filename.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
        }

        protected override WeatherObjectNew CreateAcObject(string id, bool enabled) {
            return new WeatherObjectNew(this, id, enabled);
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.WeatherDirectories;
    }
}
