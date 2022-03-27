using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class LuaAppContentEntry : ContentEntryBase<LuaAppObject> {
        public override double Priority => 46d;

        public LuaAppContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) {
            MoveEmptyDirectories = true;
        }

        public override string GenericModTypeName => "Lua app";
        public override string NewFormat => "New Lua app “{0}”";
        public override string ExistingFormat => "Update for the Lua app “{0}”";

        public override FileAcManager<LuaAppObject> GetManager() {
            return LuaAppsManager.Instance;
        }
    }
}