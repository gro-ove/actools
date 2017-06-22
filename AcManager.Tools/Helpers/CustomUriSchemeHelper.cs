using System;
using System.Diagnostics;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class CustomUriSchemeHelper {
        private const string UriSchemeLabel = "acmanager";
        public const string UriScheme = UriSchemeLabel + ":";

        private const string ClassName = "acmanager";
        private const string AppTitle = "AcTools Content Manager";

        private const string Version = "8";

        private const string KeyRegisteredLocation = "CustomUriSchemeHelper.RegisteredLocation";
        private const string KeyRegisteredVersion = "CustomUriSchemeHelper.RegisteredVersion";

        private static void RegisterClass(string className, string title, bool urlProtocol, int iconId) {
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{className}", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                if (key == null) return;

                if (urlProtocol) {
                    key.SetValue("URL Protocol", "", RegistryValueKind.String);
                }

                key.SetValue("", title, RegistryValueKind.String);

                using (var iconKey = key.CreateSubKey(@"DefaultIcon", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    iconKey?.SetValue("", $@"{MainExecutingFile.Location},{iconId}", RegistryValueKind.String);
                }

                using (var commandKey = key.CreateSubKey(@"shell\open\command", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    commandKey?.SetValue("", $@"{MainExecutingFile.Location} ""%1""", RegistryValueKind.String);
                }
            }
        }

        private static void RegisterExtension(string ext, string description) {
            var className = $@"{ClassName}{ext.ToLowerInvariant()}";
            RegisterClass(className, description, false, 1);

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext,
                    RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                if (key == null) return;
                using (var progIds = key.CreateSubKey(@"OpenWithProgids", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                    progIds?.SetValue(className, new byte[0], RegistryValueKind.None);
                }

                try {
                    using (var choiceKey = key.CreateSubKey(@"UserChoice", RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                        choiceKey?.SetValue("Progid", className, RegistryValueKind.String);
                    }
                } catch (Exception) {
                    // ignored
                }
            }
        }

        public static void EnsureRegistered() {
            if (MainExecutingFile.IsInDevelopment) return;

            // if (ValuesStorage.GetString(KeyRegisteredLocation) == MainExecutingFile.Location &&
            //     ValuesStorage.GetString(KeyRegisteredVersion) == Version) return;

            try {
                RegisterClass(ClassName, AppTitle, true, 0);
                RegisterExtension(@".kn5", ToolsStrings.Windows_Kn5Commentary);
                RegisterExtension(@".acreplay", ToolsStrings.Common_AcReplay);

                ValuesStorage.Set(KeyRegisteredLocation, MainExecutingFile.Location);
                ValuesStorage.Set(KeyRegisteredVersion, Version);
            } catch (Exception e) {
                Logging.Warning("Can’t register: " + e);
            }
        }
    }
}