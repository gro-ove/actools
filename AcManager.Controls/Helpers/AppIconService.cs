using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using JetBrains.Annotations;

namespace AcManager.Controls.Helpers {
    public interface IAppIconProvider {
        Uri GetTrayIcon();

        Uri GetAppIcon();
    }

    public static class AppIconService {
        private static IAppIconProvider _provider;

        public static void Initialize([NotNull] IAppIconProvider provider) {
            Debug.Assert(_provider == null, @"Already initialized");
            _provider = provider;
        }

        [NotNull]
        private static Icon ReadIcon(Uri uri) {
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo == null) throw new Exception($"Resource {uri} is missing");
            using (var iconStream = streamInfo.Stream) {
                return new Icon(iconStream);
            }
        }

        [NotNull]
        public static  Uri GetTrayIconUri() {
            return _provider.GetTrayIcon();
        }

        [NotNull]
        public static Uri GetAppIconUri() {
            return _provider.GetAppIcon();
        }

        private static Icon _trayIcon, _appIcon;

        [NotNull]
        public static  Icon GetTrayIcon() {
            return _trayIcon ?? (_trayIcon = ReadIcon(GetTrayIconUri()));
        }

        [NotNull]
        public static Icon GetAppIcon() {
            return _appIcon ?? (_appIcon = ReadIcon(GetAppIconUri()));
        }
    }
}