using System.ComponentModel;
using AcTools.Utils;

namespace AcManager.Controls.Helpers {
    [Localizable(false)]
    public static class CefSharpResolverService {
        public static readonly AssemblyResolver Resolver = new AssemblyResolver {
            Assemblies = { "CefSharp", "CefSharp.Core", "CefSharp.WinForms", "CefSharp.Wpf" }
        };
    }
}