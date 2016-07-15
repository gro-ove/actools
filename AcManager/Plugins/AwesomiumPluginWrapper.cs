using AcManager.Controls.Helpers;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Plugins {
    public class AwesomiumPluginWrapper : IPluginWrapper {
        public string Id => "Awesomium";

        public void Enable() {
            AwesomiumResolverService.Initialize(PluginsManager.Instance.GetPluginDirectory(Id));
        }

        public void Disable() {
            AwesomiumResolverService.Stop();
        }
    }
}