using System;
using System.IO;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public static class ToastWin8Helper {
        private static ToastNotifier _toastNotifier;

        public static void ShowToast(string title, string message, [NotNull] Uri icon, Action click) {
            var tempIcon = Path.Combine(Path.GetTempPath(), $"tmp_icon_{AppShortcut.AppUserModelId}.png");
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

            if (_toastNotifier == null) {
                _toastNotifier = File.Exists(AppShortcut.ShortcutLocation) && AppShortcut.AppUserModelId != null ?
                        ToastNotificationManager.CreateToastNotifier(AppShortcut.AppUserModelId) :
                        ToastNotificationManager.CreateToastNotifier();
            }

            _toastNotifier.Show(toast);
        }
    }
}