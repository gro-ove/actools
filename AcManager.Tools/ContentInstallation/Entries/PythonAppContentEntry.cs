using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class PythonAppContentEntry : ContentEntryBase<PythonAppObject> {
        public override double Priority => 45d;

        [CanBeNull]
        private readonly List<string> _icons;

        public PythonAppContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null,
                byte[] iconData = null, IEnumerable<string> icons = null) : base(true, path, id, name, version, iconData) {
            MoveEmptyDirectories = true;
            _icons = icons?.ToList();
        }

        public override string GenericModTypeName => "App";
        public override string NewFormat => "New app “{0}”";
        public override string ExistingFormat => "Update for the app “{0}”";

        public override FileAcManager<PythonAppObject> GetManager() {
            return PythonAppsManager.Instance;
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var callback = base.GetCopyCallback(destination);
            var icons = _icons;
            if (icons == null) return callback;

            return new CopyCallback(info => {
                var b = callback?.File(info);
                return b != null || !icons.Contains(info.Key) ? b :
                        Path.Combine(AcPaths.GetGuiIconsFilename(AcRootDirectory.Instance.RequireValue),
                                Path.GetFileName(info.Key) ?? "icon.tmp");
            }, info => callback?.Directory(info));
        }
    }
}