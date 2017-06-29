using AcManager.Tools.Managers.Plugins;

namespace AcManager.AcSound {
    public class FmodPluginWrapper : IPluginWrapper {
        public const string IdValue = "Fmod";

        public string Id => IdValue;

        public void Enable() {
            EnsureEnabled();
        }

        public void Disable() {
            EnsureDisabled();
        }

        public static void EnsureEnabled() {
            if (FmodResolverService.IsInitialized) return;
            FmodResolverService.Initialize(PluginsManager.Instance.GetPluginDirectory(IdValue));
        }

        public static void EnsureDisabled() {
            if (!FmodResolverService.IsInitialized) return;
            FmodResolverService.Stop();
        }
    }
}