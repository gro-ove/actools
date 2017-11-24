using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Tools.Managers.Plugins;

namespace AcManager.Plugins {
    public class CefSharpPluginWrapper : IPluginWrapper {
        public string Id => CefSharpPluginInformation.Id;

        public void Enable() {
            EnsureEnabled();
        }

        public void Disable() {
            EnsureDisabled();
        }

        public static void EnsureEnabled() {
            if (CefSharpResolverService.IsInitialized) return;
            CefSharpResolverService.Initialize(PluginsManager.Instance.GetPluginDirectory(CefSharpPluginInformation.Id));
        }

        public static void EnsureDisabled() {
            if (!CefSharpResolverService.IsInitialized) return;
            CefSharpResolverService.Stop();
        }
    }
}