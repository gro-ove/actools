using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.UserControls.CefSharp {
    /// <summary>
    /// Vague attempt to improve poor performance of those Chromium-based engines
    /// at least a little bit.
    /// </summary>
    internal static class RequestsFiltering {
        private static readonly Regex Regex = new Regex(@"^
                https?://(?:
                    apis\.google\.com/se/0/_/\+1 |
                    connect\.facebook\.net |
                    cdn\.viglink\.com/images/pixel\.gif |
                    mc\.yandex\.ru |
                    pagead2\.googlesyndication\.com/pagead |
                    platform\.twitter\.com/widgets |
                    ssl\.google-analytics\.com |
                    staticxx\.facebook\.com/connect |
                    syndication\.twitter\.com/i/jot |
                    [a-z]+\.(?:adsnative\.com|g\.doubleclick\.net) |
                    x\.bidswitch\.net |
                    www\.(?:
                        google-analytics\.com |
                        facebook\.com/(?:connect/ping|plugins/like\.php) |
                        youtube\.com/subscribe_embed
                    )
                )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static bool ShouldBeBlocked(string url) {
            if (!SettingsHolder.Plugins.CefFilterAds) return false;

#if DEBUG
            if (Regex.IsMatch(url)) {
                Logging.Warning(url);
                return true;
            }

            Logging.Debug(url);
            return false;
#else
            return Regex.IsMatch(url);
#endif
        }
    }
}