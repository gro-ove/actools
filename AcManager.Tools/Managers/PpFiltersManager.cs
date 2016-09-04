using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class PpFiltersManager : AcManagerFileSpecific<PpFilterObject> {
        public static PpFiltersManager Instance { get; private set; }

        public static PpFiltersManager Initialize() {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new PpFiltersManager();
        }

        [CanBeNull]
        public PpFilterObject GetByAcId(string v) {
            return GetById(v + PpFilterObject.FileExtension);
        }

        public string DefaultFilename => Path.Combine(Directories.EnabledDirectory, "default.ini");

        public override string SearchPattern => @"*.ini";

        public override PpFilterObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"default"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.PpFiltersDirectories;

        protected override PpFilterObject CreateAcObject(string id, bool enabled) {
            return new PpFilterObject(this, id, enabled);
        }
    }
}
