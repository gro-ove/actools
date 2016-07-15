using System;
using System.Drawing;
using System.Windows.Forms;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Application = System.Windows.Application;

namespace AcManager.Controls.Helpers {
    public static class Toast {
        public static bool OptionFallbackMode = false;

        private static bool _winToasterIsNotAvailable;
        private static Uri _defaultIcon;

        public static void SetDefaultIcon(Uri iconUri) {
            _defaultIcon = iconUri;
        }

        private static Action _defaultAction;

        public static void SetDefaultAction(Action defaultAction) {
            _defaultAction = defaultAction;
        }

        /// <summary>
        /// Show a toast.
        /// </summary>
        /// <param name="title">Ex.: “Something Happened”</param>
        /// <param name="message">Ex.: “This and that. Without dot in the end”</param>
        /// <param name="click">Click action</param>
        public static void Show(string title, string message, Action click = null) {
            if (_defaultIcon == null) return;
            Show(title, message, _defaultIcon, click ?? _defaultAction);
        }

        /// <summary>
        /// Show a toast.
        /// </summary>
        /// <param name="title">Ex.: “Something Happened”</param>
        /// <param name="message">Ex.: “This and that. Without dot in the end”</param>
        /// <param name="icon">Uri to some icon</param>
        /// <param name="click">Click action</param>
        public static void Show(string title, string message, [NotNull] Uri icon, Action click = null) {
            if (!_winToasterIsNotAvailable && !OptionFallbackMode) {
                try {
                    ToastWin8Helper.Instance.ShowToast(title, message, icon, click ?? _defaultAction);
                    return;
                } catch (Exception e) {
                    Logging.Warning("[TOAST] Win8 Toaster is not available: " + e);
                    _winToasterIsNotAvailable = true;
                }
            }

            ShowFallback(title, message, icon, click);
        }

        private static Icon _fallbackIcon;

        private static void ShowFallback(string title, string message, [NotNull] Uri icon, Action click) {
            if (_fallbackIcon == null) {
                using (var iconStream = System.Windows.Application.GetResourceStream(icon)?.Stream) {
                    _fallbackIcon = iconStream == null ? null : new Icon(iconStream);
                }
            }
            
            using (var notifyIcon = new NotifyIcon {
                Icon = _fallbackIcon,
                Text = title + "\n" + message
            }) {
                notifyIcon.Visible = true;
                if (click != null) {
                    notifyIcon.BalloonTipClicked += (sender, args) => {
                        Application.Current.Dispatcher.Invoke(click);
                    };
                }
                notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
                notifyIcon.Visible = false;
            }
        }
    }
}
