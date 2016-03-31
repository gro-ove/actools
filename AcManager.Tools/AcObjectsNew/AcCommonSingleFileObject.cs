using System;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcCommonSingleFileObject : AcCommonObject {
        public abstract string Extension { get; }

        public string Filename { get; }

        public sealed override string Location => base.Location;

        protected AcCommonSingleFileObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            Filename = Path.GetFileName(Location);
        }

        public string GetOriginalFilename() {
            var fileInfo = new FileInfo(Location);
            var result = fileInfo.Directory?.GetFiles(fileInfo.Name)[0].Name ?? Location;
            return Extension != "" && result.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ?
                result.Substring(0, result.Length - Extension.Length) :
                result;
        }

        protected override void LoadOrThrow() {
            Name = GetOriginalFilename();
        }

        public override void Save() {
            FileUtils.Move(Location, FileAcManager.Directories.GetLocation(Name + ".ini", Enabled));
        }

        public override bool HandleChangedFile(string filename) {
            return true;
        }
    }
}
