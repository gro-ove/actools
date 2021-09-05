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
using AcManager.Pages.Windows;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class TrackTitan : ILoadableContent {
        private static readonly Uri TrackTitanUri = new Uri("/Pages/Drive/TrackTitan.xaml", UriKind.Relative);
        private static string _loginToken;

        public static void NavigateTo(string loginToken = null) {
            _loginToken = loginToken ?? _loginToken;
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(TrackTitanUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(TrackTitanUri);
                    break;
            }
        }

        public TrackTitan() {
            this.SetCustomAccentColor(Color.FromArgb(255, 241, 134, 13));
        }

        public static PluginsRequirement Requirement { get; } = new PluginsRequirement(KnownPlugins.CefSharp);

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await Task.Yield();
            // await CarsManager.Instance.EnsureLoadedAsync();
            // await TracksManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            // CarsManager.Instance.EnsureLoaded();
            // TracksManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged { }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class TrackTitanApiProxy : JsGenericProxy {
            public TrackTitanApiProxy(JsBridgeBase bridge) : base(bridge) { }
        }

        public class TrackTitanApiBridge : JsBridgeBase {
            public TrackTitanApiBridge() {
                AcApiHosts.Add(@"tracktitan.io");
                AcApiHosts.Add(@"www.tracktitan.io");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new TrackTitanApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var browser = (WebBlock)sender;
            browser.SetJsBridge<TrackTitanApiBridge>();
        }
    }
}