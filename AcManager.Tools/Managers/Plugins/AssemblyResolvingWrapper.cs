using AcManager.Tools.Helpers;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Plugins {
    public class AssemblyResolvingWrapper : IPluginWrapper {
        public string Id { get; }

        private readonly AssemblyResolver _resolver;

        public AssemblyResolvingWrapper([NotNull] string id, AssemblyResolver resolver) {
            Id = id;
            _resolver = resolver;
            _resolver.Error += OnResolverError;
        }

        private void OnResolverError(object sender, AssemblyResolverErrorEventArgs args) {
            if (VisualCppTool.OnException(args.Exception, null)) {
                args.Handled = true;
            }
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