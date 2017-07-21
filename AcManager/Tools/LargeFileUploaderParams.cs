using System;
using System.Diagnostics;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools {
    public static class LargeFileUploaderParams {
        private static string _filename;
        private static bool _disableCompression;

        private static Storage _storage;
        public static Storage Storage => _storage ?? (_storage = new Storage(_filename, "pzni2j4i" + SteamIdHelper.Instance.Value, _disableCompression));

        public static void Initialize(string filename, bool disableCompression = false) {
            Debug.Assert(_storage == null);
            _filename = filename;
            _disableCompression = disableCompression;
        }

        private static UploaderParams _sharing;
        public static UploaderParams Sharing => _sharing ?? (_sharing = new UploaderParams(Storage.GetSubstorage("sharing:")));

        private static UploaderParams _serverPresets;
        public static UploaderParams ServerPresets => _serverPresets ?? (_serverPresets = new UploaderParams(Storage.GetSubstorage("serverPresets:")));
    }
}