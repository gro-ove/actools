using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Pages.Windows;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class UnitedRacingData : ILoadableContent {
        private static readonly Uri UnitedRacingDataUri = new Uri("/Pages/Drive/UnitedRacingData.xaml", UriKind.Relative);
        private static string _loginToken;

        public static void NavigateTo(string loginToken = null) {
            _loginToken = loginToken ?? _loginToken;
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(UnitedRacingDataUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(UnitedRacingDataUri);
                    break;
            }
        }

        public static PluginsRequirement Requirement { get; } = new PluginsRequirement(KnownPlugins.CefSharp);

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await Task.Yield();
        }

        public void Load() { }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged { }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class UnitedRacingDataApiProxy : JsGenericProxy {
            public UnitedRacingDataApiProxy(JsBridgeBase bridge) : base(bridge) { }
        }

        public class UnitedRacingDataApiBridge : JsBridgeBase {
            public UnitedRacingDataApiBridge() {
                AcApiHosts.Add(@"unitedracingdata.com");
                AcApiHosts.Add(@"www.unitedracingdata.com");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new UnitedRacingDataApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var browser = (WebBlock)sender;
            browser.SetJsBridge<UnitedRacingDataApiBridge>();
        }
    }
}