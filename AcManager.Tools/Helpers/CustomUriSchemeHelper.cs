using System;
using System.ComponentModel;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class CustomUriSchemeHelper {
        public static bool OptionRegisterEachTime = true;

        private const string UriSchemeLabel = "acmanager";
        public const string UriScheme = UriSchemeLabel + ":";

        private const string ClassName = "acmanager";
        private const string AppTitle = "AcTools Content Manager";

        private const string Version = "8";

        private const string KeyRegisteredLocation = "CustomUriSchemeHelper.RegisteredLocation";
        private const string KeyRegisteredVersion = "CustomUriSchemeHelper.RegisteredVersion";

        private static void RegisterClass(string className, string title, bool urlProtocol, int iconId, bool isEnabled) {
            var path = $@"Software\Classes\{className}";
            if (!isEnabled) {
                Registry.CurrentUser.DeleteSubKeyTree(path);
                return;
            }

            using (var key = Registry.CurrentUser.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree)) {
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

        private static void RegisterExtension(string ext, string description, bool isEnabled, int iconId) {
            var className = $@"{ClassName}{ext.ToLowerInvariant()}";
            RegisterClass(className, description, false, iconId, isEnabled);

            var path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext;
            if (!isEnabled) {
                Registry.CurrentUser.DeleteSubKeyTree(path);
                return;
            }

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

        private static void EnsureRegistered() {
            if (MainExecutingFile.IsInDevelopment) return;

            try {
                var isEnabled = SettingsHolder.Common.IsRegistryEnabled;
                var isFilesIntegrationEnabled = SettingsHolder.Common.IsRegistryFilesIntegrationEnabled;

                if (SettingsHolder.Common.IsRegistryEnabled) {
                    RegisterClass(ClassName, AppTitle, true, 0, isEnabled);
                    RegisterExtension(@".kn5", ToolsStrings.Windows_Kn5Commentary, isFilesIntegrationEnabled, 1);
                    RegisterExtension(@".acreplay", ToolsStrings.Common_AcReplay, isFilesIntegrationEnabled, 2);
                }

                ValuesStorage.Set(KeyRegisteredLocation, MainExecutingFile.Location);
                ValuesStorage.Set(KeyRegisteredVersion, Version);
            } catch (Exception e) {
                Logging.Warning("Can’t register: " + e);
            }
        }

        public static void Initialize() {
            SettingsHolder.Common.PropertyChanged += OnSettingsChanged;
            if (!MainExecutingFile.IsInDevelopment && (OptionRegisterEachTime || ValuesStorage.GetString(KeyRegisteredLocation) != MainExecutingFile.Location
                    && ValuesStorage.GetString(KeyRegisteredVersion) != Version)) {
                EnsureRegistered();
            }
        }

        private static void OnSettingsChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs.PropertyName == nameof(SettingsHolder.CommonSettings.RegistryMode)) {
                EnsureRegistered();
            }
        }
    }
}