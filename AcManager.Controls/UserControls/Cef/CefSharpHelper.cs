using System;
using System.IO;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers.Plugins;
using CefSharp;
using CefSharp.Wpf;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls.Cef {
    internal static class CefSharpHelper {
        public static bool OptionMultiThreadedMessageLoop = true;

        #region Initialization
        internal static readonly string DefaultUserAgent;

        static CefSharpHelper() {
            DefaultUserAgent = CmApiProvider.CommonUserAgent;
            AcApiHandler = new AcApiHandlerFactory();
            // CefSharp.Cef.EnableHighDPISupport();
        }
        #endregion

        internal static readonly AcApiHandlerFactory AcApiHandler;

        internal static void EnsureInitialized() {
            if (!CefSharp.Cef.IsInitialized) {
                var wpfMode = !SettingsHolder.Plugins.CefWinForms;

                Logging.Write($"Initializing CEF (WPF mode: {wpfMode})…");

                // TODO: Try the new way?
                CefSharpSettings.LegacyJavascriptBindingEnabled = true;
                CefSharpSettings.WcfEnabled = true;
                CefSharp.Cef.EnableHighDPISupport();

                var path = PluginsManager.Instance.GetPluginDirectory(KnownPlugins.CefSharp);
                var settings = new CefSettings {
                    UserAgent = DefaultUserAgent,
                    LogSeverity = LogSeverity.Disable,
                    CachePath = FilesStorage.Instance.GetTemporaryFilename(@"Cef"),
                    UserDataPath = FilesStorage.Instance.GetTemporaryFilename(@"Cef"),
                    BrowserSubprocessPath = Path.Combine(path, "CefSharp.BrowserSubprocess.exe"),
                    LocalesDirPath = Path.Combine(path, "locales"),
                    ResourcesDirPath = Path.Combine(path),
                    Locale = SettingsHolder.Locale.LocaleName,
#if DEBUG
                    RemoteDebuggingPort = 45451,
#endif
                };

                if (wpfMode) {
                    settings.MultiThreadedMessageLoop = OptionMultiThreadedMessageLoop;
                    settings.ExternalMessagePump = !OptionMultiThreadedMessageLoop;
                    settings.WindowlessRenderingEnabled = true;
                    settings.SetOffScreenRenderingBestPerformanceArgs();
                }

                settings.RegisterScheme(new CefCustomScheme {
                    SchemeName = AcApiHandlerFactory.AcSchemeName,
                    IsCSPBypassing = true,
                    IsDisplayIsolated = false,
                    IsLocal = false,
                    IsSecure = false,
                    IsStandard = false,
                    SchemeHandlerFactory = AcApiHandler
                });

                settings.RegisterScheme(new CefCustomScheme {
                    SchemeName = AltFilesHandlerFactory.SchemeName,
                    IsCSPBypassing = true,
                    IsDisplayIsolated = false,
                    IsLocal = false,
                    IsSecure = false,
                    IsStandard = false,
                    SchemeHandlerFactory = new AltFilesHandlerFactory()
                });

                AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                    try {
                        CefSharp.Cef.Shutdown();
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                };

                CefSharp.Cef.Initialize(settings, false,
                        wpfMode && !OptionMultiThreadedMessageLoop ? new WpfBrowserProcessHandler(Application.Current.Dispatcher) : new BrowserProcessHandler());
                Logging.Write("CEF is initialized");
            }
        }
    }
}