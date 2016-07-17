using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class WeatherManager : AcManagerNew<WeatherObject> {
        public static WeatherManager Instance { get; private set; }

        public static WeatherManager Initialize() {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new WeatherManager();
        }

        private static readonly string[] WatchedFiles = {
            // @"colorCurves.ini",
            @"weather.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.WeatherDirectories;

        public override WeatherObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"clear"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        protected override WeatherObject CreateAcObject(string id, bool enabled) {
            return new WeatherObject(this, id, enabled);
        }
    }
}
