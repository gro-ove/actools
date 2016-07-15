using System;
using System.IO;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Helpers {
    public class ToastWin8Helper {
        public const string AppUserModelId = "AcClub.ContentManager";
        public static string ShortcutLocation;
        public static ToastNotifier ToastNotifier;

        private static ToastWin8Helper _instance;
        public static ToastWin8Helper Instance => _instance ?? (_instance = new ToastWin8Helper());

        public ToastWin8Helper() {
            ShortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft\Windows\Start Menu\Programs\Content Manager.lnk");

            SettingsHolder.Common.PropertyChanged += Common_PropertyChanged ;

            CreateShortcutIfMissing();
            ToastNotifier = File.Exists(ShortcutLocation) ? ToastNotificationManager.CreateToastNotifier(AppUserModelId)
                    : ToastNotificationManager.CreateToastNotifier();
        }

        private void CreateShortcutIfMissing() {
            // we need a start menu shortcut (a ShellLink object) to show toasts.
            if (File.Exists(ShortcutLocation) || !SettingsHolder.Common.CreateStartMenuShortcutIfMissing) return;

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
            } catch (Exception e) {
                Logging.Write("[TOAST] Can’t create shortcut: " + e);
            }
        }

        private void Common_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.CommonSettings.CreateStartMenuShortcutIfMissing)) {
                CreateShortcutIfMissing();
            }
        }

        public void ShowToast(string title, string message, [NotNull] Uri icon, Action click) {
            var tempIcon = FilesStorage.Instance.GetFilename("Temporary", "Icon.png");

            if (!File.Exists(tempIcon)) {
                using (var iconStream = System.Windows.Application.GetResourceStream(icon)?.Stream) {
                    if (iconStream != null) {
                        using (var file = new FileStream(tempIcon, FileMode.Create)) {
                            iconStream.CopyTo(file);
                        }
                    }
                }
            }

            var content = new XmlDocument();
            content.LoadXml("<toast><visual><binding template=\"ToastImageAndText02\"><image id=\"1\" src=\"file://" +
                    tempIcon + "\"/><text id=\"1\">" + title + "</text><text id=\"2\">" + message + "</text></binding></visual></toast>");
            var notification = new ToastNotification(content);
            if (click != null) {
                notification.Activated += (sender, args) => {
                    Application.Current.Dispatcher.Invoke(click);
                };
            }
            ToastNotifier.Show(notification);
        }
    }
}