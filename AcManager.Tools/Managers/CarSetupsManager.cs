using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class CarSetupsManager : AcManagerNew<CarSetupObject> {
        public string CarId { get; }

        public override IAcDirectories Directories { get; }

        private CarSetupsManager(string carId, IAcDirectories directories) {
            CarId = carId;
            Directories = directories;
        }

        public static CarSetupsManager Create(CarObject car) {
            return new CarSetupsManager(car.Id, new CarSetupsDirectories(car));
        }
        
        protected override string LocationToId(string directory) {
            var name = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).TakeLast(2).JoinToString(Path.DirectorySeparatorChar);
            if (name == null) throw new Exception("Cannot get file name from path");
            return name;
        }

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(CarSetupObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{CarSetupObject.FileExtension}”.";
            }

            if (id.IndexOf(Path.DirectorySeparatorChar) == -1) {
                return "ID should be in “track”\\“name” form.";
            }

            return base.CheckIfIdValid(id);
        }

        protected override string GetObjectLocation(string filename, out bool inner) {
            inner = false;
            return filename.StartsWith(Directories.EnabledDirectory + Path.DirectorySeparatorChar) ? filename : null;
        }

        public virtual string SearchPattern => @"*.ini";

        private Regex _regex;

        protected override bool Filter(string id, string filename) => SearchPattern == @"*" || (_regex ?? (_regex = new Regex(
                SearchPattern.Replace(@".", @"[.]").Replace(@"*", @".*").Replace(@"?", @"."))))
                .IsMatch(id);

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            return Directories.GetSubFiles(SearchPattern).Select(dir => {
                var id = LocationToId(dir);
                return Filter(id, dir) ? CreateAcPlaceholder(id, Directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }

        protected override CarSetupObject CreateAcObject(string id, bool enabled) {
            return new CarSetupObject(CarId, this, id, enabled);
        }
    }
}