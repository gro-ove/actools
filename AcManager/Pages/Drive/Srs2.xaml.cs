using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Properties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class Srs2 : ILoadableContent {
        public static PluginsRequirement Requirement { get; } = new PluginsRequirement(KnownPlugins.CefSharp);

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await CarsManager.Instance.EnsureLoadedAsync();
            await TracksManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            CarsManager.Instance.EnsureLoaded();
            TracksManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            InitializeComponent();
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var web = (WebBlock)sender;
            var styleProvider = new StyleProvider();
            var isThemeBright = ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).GetBrightness() > 0.4;
            web.SetJsBridge<SrsFixAcCompatibleApiBridge>(b => {
                b.StyleProvider = styleProvider;
                b.IsThemeBright = isThemeBright;
            });
            web.StyleProvider = styleProvider;
        }

        internal class StyleProvider : ICustomStyleProvider {
            public bool TransparentBackgroundSupported;

            private static string PrepareStyle(string style, bool transparentBackgroundSupported) {
                var color = AppAppearanceManager.Instance.AccentColor;

                style = style
                        .Replace(@"#E20035", color.ToHexString())
                        .Replace(@"#CA0030", ColorExtension.FromHsb(color.GetHue(), color.GetSaturation(), color.GetBrightness() * 0.92).ToHexString());
                style = Regex.Replace(style, @"(?<=^|@media).+", m => ("" + m)
                        .Replace(@"no-ads", SettingsHolder.Plugins.CefFilterAds ? @"all" : @"print")
                        .Replace(@"transparent-bg", transparentBackgroundSupported ? @"all" : @"print"));

                return style;
            }

            public string GetStyle(string url, bool transparentBackgroundSupported) {
                TransparentBackgroundSupported = transparentBackgroundSupported;
                return SettingsHolder.Live.SrsCustomStyle && url.StartsWith(@"http://www.simracingsystem.com") ?
                        PrepareStyle(BinaryResources.SrsStyle, transparentBackgroundSupported) : null;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class SrsFixAcCompatibleApiBridge : AcCompatibleApiBridge {
            public SrsFixAcCompatibleApiBridge() {
                AcApiHosts.Add(@"simracingsystem.com");
            }

            internal StyleProvider StyleProvider { get; set; }
            internal bool IsThemeBright { get; set; }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                if (IsThemeBright
                        // GetStyle() is called before PageInject(), so it’s a good way to know if browser supports
                        // transparent background or not
                        || StyleProvider?.TransparentBackgroundSupported == false) {
                    replacements.Add(new KeyValuePair<string, string>(@"<body style=""background:none;"">", @"<body>"));
                }

                base.PageInject(url, toInject, replacements);
            }
        }
    }
}