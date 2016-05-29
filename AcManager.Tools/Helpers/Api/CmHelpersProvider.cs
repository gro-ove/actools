using AcManager.Internal;

namespace AcManager.Tools.Helpers.Api {
    public class CmHelpersProvider {
        public static string GetAddress(string path) {
            return InternalUtils.CmGetAddress(path);
        }
    }
}
