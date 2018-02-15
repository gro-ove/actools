using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Plugins {
    public class AssemblyResolvingWrapper : IPluginWrapper {
        public string Id { get; }

        private readonly AssemblyResolver _resolver;

        public AssemblyResolvingWrapper([NotNull] string id, AssemblyResolver resolver) {
            Id = id;
            _resolver = resolver;
        }

        public void Enable() {
            EnsureEnabled();
        }

        public void Disable() {
            EnsureDisabled();
        }

        private void EnsureEnabled() {
            if (_resolver.IsInitialized) return;
            _resolver.Initialize(PluginsManager.Instance.GetPluginDirectory(Id));
        }

        private void EnsureDisabled() {
            if (!_resolver.IsInitialized) return;
            _resolver.Stop();
        }
    }
}