using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;

// #define LOGGING

namespace AcManager.Controls.UserControls.Cef {
    /// <summary>
    /// Vague attempt to improve poor performance of those Chromium-based engines
    /// at least a little bit.
    /// </summary>
    internal static class RequestsFiltering {
        private static readonly Regex Regex = new Regex(@"^
                https?://(?:
                    adf\.ly/funcript |
                    adsbb\.dfiles\.ru |
                    adsense\.codev\.wixapps\.net |
                    apis\.google\.com/se/0/_/\+1 |
                    back-to-top\.appspot\.com |
                    cdn\.(?:
                        adf\.ly/js/display\.js |
                        viglink\.com/images/pixel\.gif
                    ) |
                    connect\.facebook\.net |
                    disqusads\.com |
                    fonts\.googleapis\.com/.{120,} |
                    frog\.wix\.com |
                    live\.rezync\.com |
                    mc\.yandex\.ru |
                    pagead2\.googlesyndication\.com/pagead |
                    pippio\.com |
                    platform\.twitter\.com/widgets |
                    plus\.google\.com |
                    referrer\.disqus\.com |
                    sitebooster\.com |
                    ssl\.google-analytics\.com |
                    staticxx\.facebook\.com/connect |
                    syndication\.twitter\.com/i/jot |
                    wixlabs-hcounter\.appspot\.com |
                    www\.(?:
                        google-analytics\.com |
                        googletagmanager\.com
                        facebook\.com/(?:ajax/bz|connect/ping|plugins/like\.php) |
                        paypalobjects\.com/.+/pixel\.gif |
                        youtube\.com/subscribe_embed
                    ) |
                    x\.bidswitch\.net |

                    # block domains names completely
                    (?:[\w\.]+?\.)?(?:
                        addthis\.com |
                        adnxs\.com |
                        adsnative\.com |
                        acxiom\.com |
                        bluekai\.com |
                        crwdcntrl\.net |
                        doubleclick\.net |
                        exelator\.com |
                        narrative\.io |
                        rlcdn\.com |
                        v12group\.com
                    )/
                )",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static bool ShouldBeBlocked(string url) {
            if (!SettingsHolder.Plugins.CefFilterAds) return false;

#if DEBUG && LOGGING
            if (Regex.IsMatch(url)) {
                FirstFloor.ModernUI.Helpers.Logging.Warning(url);
                return true;
            }

            FirstFloor.ModernUI.Helpers.Logging.Debug(url);
            return false;
#else
            return Regex.IsMatch(url);
#endif
        }
    }
}