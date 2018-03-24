using System.Diagnostics;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Data {
    public static class AuthenticationStorage {
        private static string _filename;
        private static bool _disableCompression;

        private static Storage _storage;
        private static Storage Storage => _storage ?? (_storage = new Storage(_filename, "pzni2j4i" + SteamIdHelper.Instance.Value, _disableCompression));

        public static void Initialize(string filename, bool disableCompression = false) {
            Debug.Assert(_storage == null);
            _filename = filename;
            _disableCompression = disableCompression;
        }

        public static void Initialize() {
            Debug.Assert(_storage == null);
            _filename = null;
            _disableCompression = false;
        }

        private static IStorage _generalStorage;
        public static IStorage GeneralStorage => _generalStorage ?? (_generalStorage = new Substorage(Storage, "general:"));

        private static IStorage _shareReplaysStorage;
        public static IStorage ShareReplaysStorage => _shareReplaysStorage ?? (_shareReplaysStorage = new Substorage(Storage, "sharing:"));

        private static IStorage _shareContentStorage;
        public static IStorage ShareContentStorage => _shareContentStorage ?? (_shareContentStorage = new Substorage(Storage, "content:"));

        /*private static IStorage[] Storages => new[] { GeneralStorage, ShareReplaysStorage, ShareContentStorage };

        public static void CopyExistingTo(string[] keys, [Localizable(false)] string[] originPrefixes, IStorage destination) {
            if (keys.Any(destination.Contains)) return;
        }*/
    }
}