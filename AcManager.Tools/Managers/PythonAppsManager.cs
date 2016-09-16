using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class PythonAppsManager : AcManagerNew<PythonAppObject> {
        private static PythonAppsManager _instance;

        public static PythonAppsManager Instance => _instance ?? (_instance = new PythonAppsManager());

        protected override bool Filter(string filename) {
            return !string.Equals(Path.GetFileName(filename), @"system", StringComparison.OrdinalIgnoreCase);
        }

        public override PythonAppObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"Chat"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.PythonAppsDirectories;

        protected override PythonAppObject CreateAcObject(string id, bool enabled) {
            return new PythonAppObject(this, id, enabled);
        }
    }
}
