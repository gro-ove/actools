using System.Collections.Generic;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class FontContentEntry : ContentEntryBase<FontObject> {
        public override double Priority => 20d;

        public FontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(false, path, id, name, iconData: iconData) { }

        public override string GenericModTypeName => "Font";
        public override string NewFormat => ToolsStrings.ContentInstallation_FontNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_FontExisting;

        public override FileAcManager<FontObject> GetManager() {
            return FontsManager.Instance;
        }

        protected override ICopyCallback GetCopyCallback(string destination) {
            var bitmapExtension = Path.GetExtension(EntryPath);
            var mainEntry = EntryPath.ApartFromLast(bitmapExtension) + FontObject.FontExtension;

            return new CopyCallback(info => {
                if (FileUtils.ArePathsEqual(info.Key, mainEntry)) {
                    return destination;
                }

                if (FileUtils.ArePathsEqual(info.Key, EntryPath)) {
                    return destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                }

                return null;
            });
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything, false) };
        }
    }
}