using AcManager.Tools.Helpers;

namespace AcManager.Tools.Miscellaneous {
    public static class MagickPluginHelper {
        public static readonly string PluginId = $@"Magick-7.0.4-{BuildInformation.Platform}";
        public static readonly string AssemblyName = $"Magick.NET-Q8-{BuildInformation.Platform}.dll";
        public static readonly string NativeName = $@"Magick.NET-Q8-{BuildInformation.Platform}.Native.dll";
    }
}