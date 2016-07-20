using System.Collections.Generic;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.Plugins {
    public static class PluginsWrappers {
        private static readonly Dictionary<string, IPluginWrapper> Wrappers = new Dictionary<string, IPluginWrapper>();

        public static void Initialize(params IPluginWrapper[] wrappers) {
            PluginsManager.Instance.PluginEnabled += OnPluginEnabled;
            PluginsManager.Instance.PluginDisabled += OnPluginDisabled;

            foreach (var wrapper in wrappers) {
                Register(wrapper);
            }
        }

        public static void Register(IPluginWrapper wrapper) {
            Wrappers.Add(wrapper.Id, wrapper);
            Logging.Write("here: " + wrapper.Id);
            if (PluginsManager.Instance.IsPluginEnabled(wrapper.Id)) {
                Logging.Write("enable()");
                wrapper.Enable();
            }
        }

        private static void OnPluginDisabled(object sender, AppAddonEventHandlerArgs args) {
            IPluginWrapper wrapper;
            if (Wrappers.TryGetValue(args.PluginId, out wrapper)) {
                wrapper.Disable();
            }
        }

        private static void OnPluginEnabled(object sender, AppAddonEventHandlerArgs args) {
            IPluginWrapper wrapper;
            if (Wrappers.TryGetValue(args.PluginId, out wrapper)) {
                wrapper.Enable();
            }
        }
    }
}