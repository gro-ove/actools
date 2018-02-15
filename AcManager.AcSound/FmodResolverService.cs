using System.ComponentModel;
using AcTools.Utils;

namespace AcManager.AcSound {
    [Localizable(false)]
    public static class FmodResolverService {
        public static readonly AssemblyResolver Resolver = new AssemblyResolver {
            Assemblies = { "AcTools.SoundbankPlayer", "Fmod.Wrapper" }
        };
    }
}