using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers {
    public class FontsManager : AcManagerFileSpecific<FontObject> {
        public static FontsManager Instance { get; private set; }

        public static FontsManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new FontsManager();
        }

        public override string SearchPattern => "*.txt";

        public override FontObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains("arial"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        } 

        public override BaseAcDirectories Directories => AcRootDirectory.Instance.FontsDirectories;

        protected override FontObject CreateAcObject(string id, bool enabled) {
            return new FontObject(this, id, enabled);
        }

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            return !FontObject.BitmapExtensions.Any(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase)) &&
                   !filename.EndsWith(FontObject.FontExtension, StringComparison.OrdinalIgnoreCase);
        }

        protected override string GetObjectLocation(string filename, out bool inner) {
            var minLength = Math.Min(Directories.EnabledDirectory.Length,
                Directories.DisabledDirectory?.Length ?? int.MaxValue);

            inner = false;
            while (filename.Length > minLength) {
                var parent = Path.GetDirectoryName(filename);
                if (parent == null) return null;

                if (parent == Directories.EnabledDirectory || parent == Directories.DisabledDirectory) {
                    var special = FontObject.BitmapExtensions.FirstOrDefault(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                    if (special != null) {
                        inner = true;
                        return filename.ApartFromLast(special, StringComparison.OrdinalIgnoreCase) + FontObject.FontExtension;
                    }

                    return filename;
                }

                inner = true;
                filename = parent;
            }

            return null;
        }
    }
}