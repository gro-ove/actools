using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.Win32;
// ReSharper disable InconsistentNaming

namespace AcManager.Tools.Helpers {
    public static class WebBrowserHelper {
        public const int EmulationModeDisablingVersion = 4;

        /// <summary>
        /// Internet Explorer 11. Webpages are displayed in IE11 Standards mode, regardless of the !DOCTYPE directive.
        /// </summary>
        private const uint EmulationModeDisabled = 0x2EDF;

        public static void SetBrowserFeatureControlKey([NotNull] string feature, [NotNull] string appName, uint value) {
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            if (appName == null) throw new ArgumentNullException(nameof(appName));

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\" + feature,
                    RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                if (key == null) return;
                Logging.Write($"[WEBBROWSERHELPER] SetBrowserFeatureControlKey('{appName}', '{value}')");
                key.SetValue(appName, value, RegistryValueKind.DWord);
            }
        }

        public static void DisableBrowserEmulationMode() {
            if (MainExecutingFile.IsInDevelopment) return;
            SetBrowserFeatureControlKey("FEATURE_BROWSER_EMULATION", MainExecutingFile.Name, EmulationModeDisabled);
        }

        public static void SetSilentAlternative([NotNull] WebBrowser browser, bool silent) {
            if (browser == null) throw new ArgumentNullException(nameof(browser));

            var fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            var objComWebBrowser = fiComWebBrowser?.GetValue(browser);
            objComWebBrowser?.GetType().InvokeMember(
                    "Silent", BindingFlags.SetProperty, null, objComWebBrowser,
                    new object[] { silent });
        }

        public static void SetSilent([NotNull] WebBrowser browser, bool silent) {
            if (browser == null) throw new ArgumentNullException(nameof(browser));

            var sp = browser.Document as IOleServiceProvider;
            if (sp == null) return;

            var iidIWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
            var iidIWebBrowser2 = new Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E");

            object webBrowser;
            sp.QueryService(ref iidIWebBrowserApp, ref iidIWebBrowser2, out webBrowser);
            webBrowser?.GetType().InvokeMember("Silent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.PutDispProperty,
                    null, webBrowser, new object[] { silent });
        }

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleServiceProvider {
            [PreserveSig]
            int QueryService([In] ref Guid guidService, [In] ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object ppvObject);
        }

        [DllImport("urlmon.dll", CharSet = CharSet.Ansi)]
        private static extern int UrlMkSetSessionOption(
                int dwOption, string pBuffer, int dwBufferLength, int dwReserved);

        const int URLMON_OPTION_USERAGENT = 0x10000001;
        const int URLMON_OPTION_USERAGENT_REFRESH = 0x10000002;

        public static void SetUserAgent(string userAgent) {
            UrlMkSetSessionOption(URLMON_OPTION_USERAGENT_REFRESH, null, 0, 0);
            UrlMkSetSessionOption(URLMON_OPTION_USERAGENT, userAgent, userAgent.Length, 0);
        }
    }
}
