using System;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcCommonSingleFileObject : AcCommonObject {
        public abstract string Extension { get; }

        public sealed override string Location => base.Location;

        protected AcCommonSingleFileObject(IFileAcManager manager, string fileName, bool enabled)
                : base(manager, fileName, enabled) {}

        public string GetOriginalFileName() {
            var fileInfo = new FileInfo(Location);
            var result = fileInfo.Directory?.GetFiles(fileInfo.Name)[0].Name ?? Location;
            return result;
        }

        private string _originalName;

        protected override void LoadOrThrow() {
            Name = FileName.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            _originalName = Name;
        }

        protected virtual void Rename() {
            // to keep upper case if needed
            var destination = Path.GetDirectoryName(FileAcManager.Directories.GetLocation(FileName, Enabled));
            if (destination == null) throw new Exception("Invalid destination");
            FileUtils.Move(Location, Path.Combine(destination, FileName));
        }

        public override void Save() {
            if (_originalName != Name) {
                Rename();
                _originalName = Name;
            }
        }

        public override bool HandleChangedFile(string filename) {
            return true;
        }
    }
}
