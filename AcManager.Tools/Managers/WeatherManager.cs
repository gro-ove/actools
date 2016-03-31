using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class WeatherManager : AcManagerNew<WeatherObject> {
        public static WeatherManager Instance { get; private set; }

        public static WeatherManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new WeatherManager();
        }

        protected override bool ShouldSkipFileInternal(string filename) {
            return !filename.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.WeatherDirectories;

        public override WeatherObject GetDefault() {
            return EnsureWrapperLoaded(WrappersList.FirstOrDefault(x => x.Value.Id.Contains("clear"))) ?? base.GetDefault();
        }

        protected override WeatherObject CreateAcObject(string id, bool enabled) {
            return new WeatherObject(this, id, enabled);
        }
    }
}
