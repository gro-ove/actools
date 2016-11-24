using AcManager.Controls.Helpers;
using AcManager.Tools.Managers.Plugins;

namespace AcManager.Plugins {
    public class CefSharpPluginWrapper : IPluginWrapper {
        public const string IdValue = "CefSharp";

        public string Id => IdValue;

        public void Enable() {
            EnsureEnabled();
        }

        public void Disable() {
            EnsureDisabled();
        }

        public static void EnsureEnabled() {
            if (CefSharpResolverService.IsInitialized) return;
            CefSharpResolverService.Initialize(PluginsManager.Instance.GetPluginDirectory(IdValue));
        }

        public static void EnsureDisabled() {
            if (!CefSharpResolverService.IsInitialized) return;
            CefSharpResolverService.Stop();
        }
    }
}