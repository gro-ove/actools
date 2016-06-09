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

        protected override string GetObjectLocation(string filename, out bool inner) {
            inner = false;
            return filename.StartsWith(Directories.EnabledDirectory + Path.DirectorySeparatorChar) ? filename : null;
        }

        public virtual string SearchPattern => "*.ini";

        private Regex _regex;

        protected override bool Filter(string filename) => SearchPattern == "*" || (_regex ?? (_regex = new Regex(
                SearchPattern.Replace(".", "[.]").Replace("*", ".*").Replace("?", "."))))
                .IsMatch(Path.GetFileName(filename) ?? "");

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return Directories.GetSubFiles(SearchPattern).Where(Filter).Select(dir =>
                    CreateAcPlaceholder(LocationToId(dir), Directories.CheckIfEnabled(dir)));
        }

        protected override CarSetupObject CreateAcObject(string id, bool enabled) {
            return new CarSetupObject(CarId, this, id, enabled);
        }
    }
}