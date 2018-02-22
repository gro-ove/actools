using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
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

        private void OnWebClockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge(() => new AcCompatibleApiBridge {
                AcApiHosts = { @"simracingsystem.com" }
            });
        }
    }
}