using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class PpFiltersManager : AcManagerFileSpecific<PpFilterObject> {
        private static PpFiltersManager _instance;

        public static PpFiltersManager Instance => _instance ?? (_instance = new PpFiltersManager());

        [CanBeNull]
        public PpFilterObject GetByAcId(string v) {
            return GetById(v + PpFilterObject.FileExtension);
        }

        public string DefaultFilename => Directories.GetLocation("default.ini", true);

        public override string SearchPattern => @"*.ini";

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(PpFilterObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{PpFilterObject.FileExtension}”.";
            }

            return base.CheckIfIdValid(id);
        }

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