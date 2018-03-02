using AcTools;

namespace AcManager.Tools.Managers.Plugins {
    public static class KnownPlugins {
        public static readonly string CefSharp = $@"CefSharp-63.0.0-{BuildInformation.Platform}";
        public static readonly string Fann = $@"Fann-{BuildInformation.Platform}";
        public static readonly string Fmod = @"Fmod";
        public static readonly string Magick = $@"Magick-7.0.4-{BuildInformation.Platform}";
        public static readonly string SevenZip = @"7Zip";
        public static readonly string ImageMontage = @"ImageMontage";
    }
}