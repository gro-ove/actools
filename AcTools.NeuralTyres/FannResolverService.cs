using System.ComponentModel;
using AcTools.Utils;

namespace AcTools.NeuralTyres {
    [Localizable(false)]
    public static class FannResolverService {
        public static readonly AssemblyResolver Resolver = new AssemblyResolver {
            Assemblies = { "FANNCSharp.Double" },
            Imports = { "fanndouble" }
        };
    }
}