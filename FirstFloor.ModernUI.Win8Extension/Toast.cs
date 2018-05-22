#if WIN8SUPPORTED

using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace FirstFloor.ModernUI.Win8Extension {
    public static class Toast {
        private static ToastNotifier _toastNotifier;

        public static void Show(ToastParameters parameters) {
            FileInfo tempIcon;
            if (parameters.IconUri != null && parameters.AppUserModelId != null) {
                tempIcon = new FileInfo(Path.Combine(Path.GetTempPath(), $"tmp_icon_{parameters.AppUserModelId}.png"));
                if (!tempIcon.Exists || DateTime.Now - tempIcon.LastWriteTime > TimeSpan.FromDays(7)) {
                    using (var iconStream = Application.GetResourceStream(parameters.IconUri)?.Stream) {
                        if (iconStream != null) {
                            using (var file = new FileStream(tempIcon.FullName, FileMode.Create)) {
                                iconStream.CopyTo(file);
                            }
                        }
                    }
                }
            } else {
                tempIcon = null;
            }

            var content = new XmlDocument();
            content.LoadXml(tempIcon == null ? $@"<toast>
    <visual>
        <binding template=""ToastText02"">
            <text id=""1"">{parameters.Title}</text>
            <text id=""2"">{parameters.Message}</text>
        </binding>
    </visual>
</toast>"
                    : $@"<toast>
    <visual>
        <binding template=""ToastImageAndText02"">
            <image id=""1"" src=""file://{tempIcon.FullName}""/>
            <text id=""1"">{parameters.Title}</text>
            <text id=""2"">{parameters.Message}</text>
        </binding>
    </visual>
</toast>");

            var toast = new ToastNotification(content);
            if (parameters.ClickCallback != null) {
                toast.Activated += (sender, args) => (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(parameters.ClickCallback);
            }

            if (_toastNotifier == null) {
                _toastNotifier = parameters.BoundToCallingApplication || parameters.AppUserModelId == null ?
                        ToastNotificationManager.CreateToastNotifier() :
                        ToastNotificationManager.CreateToastNotifier(parameters.AppUserModelId);
            }

            _toastNotifier.Show(toast);
        }
    }
}

#else

namespace FirstFloor.ModernUI.Win8Extension {
    public static class Toast {
        public static void Show(string title, string message, object icon, object click) {
            throw new System.NotSupportedException();
        }
    }
}

#endif