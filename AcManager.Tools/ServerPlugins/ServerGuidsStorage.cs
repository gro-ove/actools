using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ServerPlugins {
    // Saves user names for GUIDs (Steam IDs) encountered when hosting online servers, so list of banned
    // users could be more than just a bunch of numbers.
    public static class ServerGuidsStorage {
        private static Storage _guidsStorage;

        private static Storage GetGuidsStorage() {
            return _guidsStorage ?? (_guidsStorage = new Storage(FilesStorage.Instance.GetFilename("Online GUIDs.data")));
        }

        [CanBeNull]
        public static string GetAssociatedUserName([NotNull] string guid) {
            return GetGuidsStorage().Get<string>(guid);
        }

        public static void RegisterUserName([NotNull] string guid, [CanBeNull] string userName) {
            if (userName != null) {
                GetGuidsStorage().Set(guid, userName);
            }
        }
    }
}