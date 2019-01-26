using System.ComponentModel;
using AcManager.Internal;

namespace AcManager.Tools.Helpers.Api {
    public class CmHelpersProvider {
        public static string GetAddress([Localizable(false)] string path) {
            return InternalUtils.CmGetApiAddress(path);
        }
    }
}
