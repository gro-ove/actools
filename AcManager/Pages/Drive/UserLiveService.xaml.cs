using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class UserLiveService : IParametrizedUriContent, ILoadableContent {
        private string _url;

        public void OnUri(Uri uri) {
            _url = uri.GetQueryParam("url");
            var entry = SettingsHolder.Live.UserEntries.GetByIdOrDefault(uri.GetQueryParam("url"));
            if (entry?.HighlightColor != null) {
                this.SetCustomAccentColor(entry.HighlightColor.Value);
            } else {
                var color = uri.GetQueryParam("color");
                if (!string.IsNullOrEmpty(color)) {
                    this.SetCustomAccentColor(color.As(Colors.YellowGreen));
                } else {
                    Browser.PreferTransparentBackground = false;
                }
            }
            Browser.StartPage = _url;
            Browser.KeepAliveKey = "ulsk:" + _url;
            Browser.SaveKey = "ulss:" + _url;
        }

        private static PluginsRequirement _requirement;
        public static PluginsRequirement Requirement => _requirement ?? (_requirement = new PluginsRequirement(KnownPlugins.CefSharp));

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await CarsManager.Instance.EnsureLoadedAsync();
            await TracksManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            CarsManager.Instance.EnsureLoaded();
            TracksManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged { }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class UserLiveServiceApiProxy : JsGenericProxy {
            public UserLiveServiceApiProxy(JsBridgeBase bridge) : base(bridge) { }
        }

        public class UserLiveServiceApiBridge : JsBridgeBase {
            public void AddAllowedHosts(string domainName) {
                AcApiHosts.Add(domainName);
                AcApiHosts.Add(@"." + domainName);
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new UserLiveServiceApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge<UserLiveServiceApiBridge>(bridge => bridge.AddAllowedHosts(_url.GetDomainNameFromUrl()));
        }
    }
}