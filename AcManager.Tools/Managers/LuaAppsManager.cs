using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class LuaAppsManager : AcManagerNew<LuaAppObject> {
        private static LuaAppsManager _instance;

        public static LuaAppsManager Instance => _instance ?? (_instance = new LuaAppsManager());

        private LuaAppsManager() {
            CupClient.Register(this, CupContentType.LuaApp);
        }

        public override LuaAppObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"Chat"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.LuaAppsDirectories;

        protected override LuaAppObject CreateAcObject(string id, bool enabled) {
            return new LuaAppObject(this, id, enabled);
        }
    }
}