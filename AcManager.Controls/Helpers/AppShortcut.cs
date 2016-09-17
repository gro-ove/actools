using System;
using System.IO;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Helpers {
    public static class AppShortcut {
        public static readonly string AppUserModelId = "AcClub.ContentManager";
        public static readonly string ShortcutLocation;

        static AppShortcut() {
            ShortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft\Windows\Start Menu\Programs\Content Manager.lnk");
        }

        public static DelegateCommand CreateShortcutCommand { get; } = new DelegateCommand(CreateShortcut, () => !HasShortcut());

        public static DelegateCommand DeleteShortcutCommand { get; } = new DelegateCommand(DeleteShortcut, HasShortcut);

        public static bool HasShortcut() {
            if (!File.Exists(ShortcutLocation)) return false;
            using (var shortcut = new ShellLink(ShortcutLocation)) {
                return shortcut.AppUserModelID == AppUserModelId;
            }
        }

        public static void DeleteShortcut() {
            if (File.Exists(ShortcutLocation)) {
                File.Delete(ShortcutLocation);
                CreateShortcutCommand.RaiseCanExecuteChanged();
                DeleteShortcutCommand.RaiseCanExecuteChanged();
            }
        }

        public static void CreateShortcut() {
            if (HasShortcut()) return;

            try {
                var directory = Path.GetDirectoryName(ShortcutLocation) ?? "";
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                using (var shortcut = new ShellLink()) {
                    shortcut.TargetPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                    shortcut.Arguments = "";
                    shortcut.AppUserModelID = AppUserModelId;
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