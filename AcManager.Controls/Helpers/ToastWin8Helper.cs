using System;
using System.IO;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Controls.Helpers {
    public static class ToastWin8Helper {
        public static readonly ToastNotifier ToastNotifier;

        static ToastWin8Helper() {
            ToastNotifier = File.Exists(AppShortcut.ShortcutLocation) ? ToastNotificationManager.CreateToastNotifier(AppShortcut.AppUserModelId)
                    : ToastNotificationManager.CreateToastNotifier();
        }

        public static void ShowToast(string title, string message, [NotNull] Uri icon, Action click) {
            var tempIcon = FilesStorage.Instance.GetFilename("Temporary", "Icon.png");

            if (!File.Exists(tempIcon)) {
                using (var iconStream = Application.GetResourceStream(icon)?.Stream) {
                    if (iconStream != null) {
                        using (var file = new FileStream(tempIcon, FileMode.Create)) {
                            iconStream.CopyTo(file);
                        }
                    }
                }
            }

            var content = new XmlDocument();
            content.LoadXml($@"<toast>
    <visual>
        <binding template=""ToastImageAndText02"">
            <image id=""1"" src=""file://{tempIcon}""/>
            <text id=""1"">{title}</text>
            <text id=""2"">{message}</text>
        </binding>
    </visual>
</toast>");

            var toast = new ToastNotification(content);
            if (click != null) {
                toast.Activated += (sender, args) => {
                    click.InvokeInMainThreadAsync();
                };
            }

            ToastNotifier.Show(toast);
        }
    }
}