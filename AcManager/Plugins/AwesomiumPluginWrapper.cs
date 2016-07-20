using AcManager.Controls.Helpers;
using AcManager.Tools.Managers.Plugins;

namespace AcManager.Plugins {
    public class AwesomiumPluginWrapper : IPluginWrapper {
        public const string IdValue = "Awesomium";

        public string Id => IdValue;

        public void Enable() {
            EnsureEnabled();
        }

        public void Disable() {
            EnsureDisabled();
        }

        public static void EnsureEnabled() {
            if (AwesomiumResolverService.IsInitialized) return;
            AwesomiumResolverService.Initialize(PluginsManager.Instance.GetPluginDirectory(IdValue));
        }

        public static void EnsureDisabled() {
            if (!AwesomiumResolverService.IsInitialized) return;
            AwesomiumResolverService.Stop();
        }
    }
}