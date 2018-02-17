using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;

namespace AcManager.Controls.UserControls.CefSharp {
    /// <summary>
    /// Vague attempt to improve poor performance of those Chromium-based engines
    /// at least a little bit.
    /// </summary>
    internal static class RequestsFiltering {
        private static readonly Regex Regex = new Regex(@"^
                https?://(?:
                    googleads\.g\.doubleclick\.net/ |
                    apis\.google\.com/se/0/_/\+1 |
                    pagead2\.googlesyndication\.com/pagead |
                    staticxx\.facebook\.com/connect |
                    syndication\.twitter\.com/i/jot |
                    platform\.twitter\.com/widgets |
                    www\.youtube\.com/subscribe_embed |
                    www\.facebook\.com/connect/ping |
                    www\.facebook\.com/plugins/like\.php )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static bool ShouldBeBlocked(string url) {
            if (!SettingsHolder.Plugins.CefFilterAds) return false;

#if DEBUG
            // Logging.Debug(url);
#endif
            return Regex.IsMatch(url);
        }
    }
}