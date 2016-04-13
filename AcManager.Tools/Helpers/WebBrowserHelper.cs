using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public static class WebBrowserHelper {
        private const uint EmulationModeDisabled = 10000;

        public static void SetBrowserFeatureControlKey(string feature, string appName, uint value) {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\" + feature,
                    RegistryKeyPermissionCheck.ReadWriteSubTree)) {
                if (key == null) return;
                key.SetValue(appName, value, RegistryValueKind.DWord);
            }
        }

        public static void DisableBrowserEmulationMode() {
            if (MainExecutingFile.IsInDevelopment) return;
            SetBrowserFeatureControlKey("FEATURE_BROWSER_EMULATION", MainExecutingFile.Name, EmulationModeDisabled);
        }

        public static void SetSilent(WebBrowser browser, bool silent) {
            if (browser == null) {
                throw new ArgumentNullException(nameof(browser));
            }
            
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
    }
}
