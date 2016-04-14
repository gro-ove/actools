using System;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class CustomUriSchemeHelper {
        private const string UriSchemeLabel = "acmanager";
        public const string UriScheme = UriSchemeLabel + ":";

        private const string ClassName = "acmanager";

        private const string Version = "3";

        private const string KeyRegisteredLocation = "CustomUriSchemeHelper.RegisteredLocation";
        private const string KeyRegisteredVersion = "CustomUriSchemeHelper.RegisteredVersion";

        private static void RegisterExtension(string ext) {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext,
                    RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                if (key == null) return;
                using (var progIds = key.CreateSubKey(@"OpenWithProgids", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    progIds?.SetValue(ClassName, new byte[0], RegistryValueKind.None);
                }

                try {
                    using (var choiceKey = key.CreateSubKey(@"UserChoice", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                        choiceKey?.SetValue("Progid", ClassName, RegistryValueKind.String);
                    }
                } catch (Exception) {
                    // ignored
                }
            }
        }
        
        public static void EnsureRegistered() {
            if (MainExecutingFile.IsInDevelopment) return;

            if (ValuesStorage.GetString(KeyRegisteredLocation) == MainExecutingFile.Location &&
                ValuesStorage.GetString(KeyRegisteredVersion) == Version) return;

            try {
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ClassName}", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    if (key == null) return;
                    key.SetValue("URL Protocol", "", RegistryValueKind.String);
                    key.SetValue("", "AcTools Content Manager", RegistryValueKind.String);

                    using (var iconKey = key.CreateSubKey(@"DefaultIcon", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                        iconKey?.SetValue("", $"{MainExecutingFile.Name},0", RegistryValueKind.String);
                    }

                    using (var commandKey = key.CreateSubKey(@"shell\open\command", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                        commandKey?.SetValue("", $@"{MainExecutingFile.Location} ""%1""", RegistryValueKind.String);
                    }
                }

                RegisterExtension(".kn5");
                RegisterExtension(".acreplay");

                ValuesStorage.Set(KeyRegisteredLocation, MainExecutingFile.Location);
                ValuesStorage.Set(KeyRegisteredVersion, Version);

                Logging.Write("[CustomUriSchemeHelper] Registered!");
            } catch (Exception e) {
                Logging.Warning("[CustomUriSchemeHelper] Can't register: " + e);
            }
        }
    }
}