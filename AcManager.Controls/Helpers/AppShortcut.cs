using System;
using System.IO;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Controls.Helpers {
    public static class AppShortcut {
        public static readonly string AppUserModelId = "AcClub.ContentManager";
        public static readonly string ShortcutLocation;

        static AppShortcut() {
            ShortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft\Windows\Start Menu\Programs\Content Manager.lnk");
        }

        public static ProperCommand CreateShortcutCommand { get; } = new ProperCommand(CreateShortcut, () => !HasShortcut());

        public static ProperCommand DeleteShortcutCommand { get; } = new ProperCommand(DeleteShortcut, HasShortcut);

        public static bool HasShortcut() {
            if (!File.Exists(ShortcutLocation)) return false;
            using (var shortcut = new ShellLink(ShortcutLocation)) {
                return shortcut.AppUserModelID == AppUserModelId;
            }
        }

        public static void DeleteShortcut() {
            if (File.Exists(ShortcutLocation)) {
                File.Delete(ShortcutLocation);
                CreateShortcutCommand.OnCanExecuteChanged();
                DeleteShortcutCommand.OnCanExecuteChanged();
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

                CreateShortcutCommand.OnCanExecuteChanged();
                DeleteShortcutCommand.OnCanExecuteChanged();
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t create shortcut", e);
            }
        }
    }
}