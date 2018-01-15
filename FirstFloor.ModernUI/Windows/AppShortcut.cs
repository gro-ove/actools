using System;
using System.ComponentModel;
using System.IO;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    [Localizable(false)]
    public static class AppShortcut {
        [CanBeNull]
        public static string AppUserModelId;
        public static string ShortcutLocation;

        public static void Initialize([NotNull] string appUserModelId, [NotNull] string shortcutName) {
            AppUserModelId = appUserModelId;
            ShortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                            $@"\Microsoft\Windows\Start Menu\Programs\{shortcutName}.lnk");
        }

        public static DelegateCommand CreateShortcutCommand { get; } = new DelegateCommand(CreateShortcut, () => !HasShortcut());

        public static DelegateCommand DeleteShortcutCommand { get; } = new DelegateCommand(DeleteShortcut, HasShortcut);

        public static bool HasShortcut() {
            if (AppUserModelId == null) throw new Exception("AppUserModelId not set");

            if (!File.Exists(ShortcutLocation)) return false;
            using (var shortcut = new ShellLink(ShortcutLocation)) {
                return shortcut.AppUserModelId == AppUserModelId;
            }
        }

        public static void DeleteShortcut() {
            if (AppUserModelId == null) throw new Exception("AppUserModelId not set");

            if (File.Exists(ShortcutLocation)) {
                File.Delete(ShortcutLocation);
                CreateShortcutCommand.RaiseCanExecuteChanged();
                DeleteShortcutCommand.RaiseCanExecuteChanged();
            }
        }

        public static void CreateShortcut() {
            try {
                if (HasShortcut()) return;

                var directory = Path.GetDirectoryName(ShortcutLocation) ?? "";
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                using (var shortcut = new ShellLink()) {
                    shortcut.TargetPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                    shortcut.Arguments = "";
                    shortcut.AppUserModelId = AppUserModelId;
                    shortcut.Save(ShortcutLocation);
                }

                CreateShortcutCommand.RaiseCanExecuteChanged();
                DeleteShortcutCommand.RaiseCanExecuteChanged();
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t create shortcut", e);
            }
        }
    }
}